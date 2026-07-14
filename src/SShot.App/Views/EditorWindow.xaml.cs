using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using SShot.App.Controls;
using SShot.App.Rendering;
using SShot.App.Resources;
using SShot.App.ViewModels;
using SShot.Core.Annotation;
using SShot.Core.Annotation.Commands;
using SShot.Core.Annotation.Shapes;
using SShot.Core.Imaging;
using SShot.Core.Settings;

namespace SShot.App.Views;

/// <summary>
/// Owns all interactive canvas editing: tool state machine, live drag feedback (drafts/ghosts
/// that bypass the document until committed), and the final RenderTargetBitmap flatten. This is
/// intentionally code-behind-heavy rather than routed through ViewModel commands - the
/// interactions need direct visual-tree/pixel access (RenderTargetBitmap, per-frame ghost
/// visuals) that doesn't naturally fit a bindable-command model.
/// </summary>
public partial class EditorWindow : Window
{
    private readonly EditorViewModel _viewModel;
    private readonly ImageFileService _imageFileService;
    private readonly SettingsService _settingsService;
    private readonly ShapePatchCache _patchCache = new();
    private SelectionAdorner? _adorner;
    private Guid? _selectedShapeId;
    private int _nextStepNumber = 1;

    private bool _isDraggingBody;
    private AnnotationShapeBase? _dragShape;
    private AnnotationShapeBase? _dragBeforeBody;
    private Point _dragLastPoint;

    private bool _isDrawingNew;
    private Point _drawStart;
    private UIElement? _draftVisual;

    private FreehandShape? _freehandDraft;
    private Polyline? _freehandGhost;

    private TextBox? _activeTextEditor;
    private Point _textEditorPosition;

    private bool _isCropDragging;
    private Point _cropStart;
    private Rectangle? _cropGhost;

    private AnnotationShapeBase? _handleDragBefore;

    private AnnotationShapeBase? _intensityDragBefore;

    /// <summary>Path of the last file this document was saved to or as (via SaveAs/Overwrite),
    /// so "Overwrite Save" knows where to write without prompting.</summary>
    private string? _currentFilePath;

    public BitmapSource? Result { get; private set; }

    public EditorWindow(EditorViewModel viewModel, ImageFileService imageFileService, SettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _imageFileService = imageFileService;
        _settingsService = settingsService;
        DataContext = viewModel;

        var image = _viewModel.Document.BaseImage;
        BaseImageView.Source = image;
        BaseImageView.Width = image.PixelWidth;
        BaseImageView.Height = image.PixelHeight;
        EditorCanvas.Width = image.PixelWidth;
        EditorCanvas.Height = image.PixelHeight;

        _viewModel.UndoRedo.StateChanged += (_, _) => RedrawShapes();

        Loaded += OnLoaded;
        RedrawShapes();
        ToolStatusText.Text = string.Format(Strings.ToolStatusFormat, ToolDisplayName(_viewModel.CurrentTool));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var layer = AdornerLayer.GetAdornerLayer(EditorCanvas);
        if (layer is not null)
        {
            _adorner = new SelectionAdorner(EditorCanvas);
            _adorner.HandleDragDelta += OnHandleDragDelta;
            _adorner.HandleDragCompleted += OnHandleDragCompleted;
            layer.Add(_adorner);
        }

        EditorCanvas.Focus();
    }

    // ---- Redraw ----

    private void RedrawShapes()
    {
        var image = _viewModel.Document.BaseImage;
        if (!ReferenceEquals(BaseImageView.Source, image))
        {
            BaseImageView.Source = image;
            BaseImageView.Width = image.PixelWidth;
            BaseImageView.Height = image.PixelHeight;
            EditorCanvas.Width = image.PixelWidth;
            EditorCanvas.Height = image.PixelHeight;
        }

        for (int i = EditorCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(EditorCanvas.Children[i], BaseImageView))
            {
                EditorCanvas.Children.RemoveAt(i);
            }
        }

        foreach (var shape in _viewModel.Document.Shapes)
        {
            EditorCanvas.Children.Add(ShapeVisualFactory.Create(shape, image, _patchCache));
        }

        _patchCache.PruneExcept(_viewModel.Document.Shapes.Select(s => s.Id));
        UpdateAdornerForSelection();
    }

    // ---- Category rail ----

    private string? _activeCategory;

    /// <summary>Each rail button shows/hides its matching detail WrapPanel (matched by Tag) in the
    /// strip above the canvas. Clicking the already-open category closes it; clicking a different
    /// one switches to it. The active button's highlight is applied via SetResourceReference (not
    /// a plain Brush) so it keeps following theme swaps made while the editor is open.</summary>
    private void OnCategoryButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string category } clicked)
        {
            return;
        }

        bool open = !string.Equals(_activeCategory, category, StringComparison.Ordinal);
        _activeCategory = open ? category : null;

        foreach (var button in CategoryRail.Children.OfType<Button>())
        {
            if (open && ReferenceEquals(button, clicked))
            {
                button.SetResourceReference(Button.BackgroundProperty, "ThemeChromeActiveBackground");
                button.SetResourceReference(Button.ForegroundProperty, "ThemeChromeActiveForeground");
            }
            else
            {
                button.ClearValue(Button.BackgroundProperty);
                button.ClearValue(Button.ForegroundProperty);
            }
        }

        foreach (var panel in CategoryPanelHost.Children.OfType<FrameworkElement>())
        {
            panel.Visibility = open && Equals(panel.Tag, category) ? Visibility.Visible : Visibility.Collapsed;
        }

        CategoryDetailPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Toolbar ----

    private void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tagName } && Enum.TryParse<EditorTool>(tagName, out var tool))
        {
            _viewModel.CurrentTool = tool;
            ToolStatusText.Text = string.Format(Strings.ToolStatusFormat, ToolDisplayName(tool));
            CommitPendingTextIfAny();
            SetSelection(null);
        }
    }

    private static string ToolDisplayName(EditorTool tool) => tool switch
    {
        EditorTool.Select => Strings.ToolSelect,
        EditorTool.Rectangle => Strings.ToolRectangle,
        EditorTool.Ellipse => Strings.ToolEllipse,
        EditorTool.Arrow => Strings.ToolArrow,
        EditorTool.Freehand => Strings.ToolFreehand,
        EditorTool.Highlighter => Strings.ToolHighlighter,
        EditorTool.Text => Strings.ToolText,
        EditorTool.StepStamp => Strings.ToolStepStamp,
        EditorTool.Mosaic => Strings.ToolMosaic,
        EditorTool.Blur => Strings.ToolBlur,
        EditorTool.Crop => Strings.ToolCrop,
        _ => tool.ToString(),
    };

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string colorName } &&
            ColorConverter.ConvertFromString(colorName) is Color color)
        {
            _viewModel.SelectedColor = color;
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var shape = GetSelectedShape();
        if (shape is null)
        {
            return;
        }

        _viewModel.UndoRedo.ExecuteAndPush(new DeleteShapeCommand(_viewModel.Document, shape));
        SetSelection(null);
    }

    private void OnBringToFrontClick(object sender, RoutedEventArgs e)
    {
        var shape = GetSelectedShape();
        if (shape is not null)
        {
            _viewModel.UndoRedo.ExecuteAndPush(new ReorderShapeCommand(_viewModel.Document, shape, _viewModel.Document.Shapes.Count - 1));
        }
    }

    private void OnSendToBackClick(object sender, RoutedEventArgs e)
    {
        var shape = GetSelectedShape();
        if (shape is not null)
        {
            _viewModel.UndoRedo.ExecuteAndPush(new ReorderShapeCommand(_viewModel.Document, shape, 0));
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        Result = Flatten();
        Clipboard.SetImage(Result);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Result = Flatten();
        var settings = _settingsService.Load();
        var format = settings.SaveAsJpeg ? ImageFileFormat.Jpeg : ImageFileFormat.Png;
        string path = _imageFileService.Save(Result, settings.SaveFolder, format);
        _currentFilePath = path;
        MessageBox.Show(this, string.Format(Strings.SavedMessageFormat, path), "SShot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        Result = Flatten();
        var settings = _settingsService.Load();
        var format = settings.SaveAsJpeg ? ImageFileFormat.Jpeg : ImageFileFormat.Png;

        var dialog = new SaveFileDialog
        {
            Title = Strings.SaveAsDialogTitle,
            Filter = Strings.ImageFileDialogFilter,
            FilterIndex = format == ImageFileFormat.Png ? 1 : 2,
            DefaultExt = format == ImageFileFormat.Png ? ".png" : ".jpg",
            FileName = _currentFilePath is null
                ? _imageFileService.BuildFileName(DateTime.Now, format)
                : System.IO.Path.GetFileName(_currentFilePath),
            InitialDirectory = Directory.Exists(settings.SaveFolder) ? settings.SaveFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _imageFileService.SaveAs(Result, dialog.FileName);
        _currentFilePath = dialog.FileName;
        MessageBox.Show(this, string.Format(Strings.SavedMessageFormat, dialog.FileName), "SShot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnOverwriteSaveClick(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath is null)
        {
            OnSaveAsClick(sender, e);
            return;
        }

        Result = Flatten();
        _imageFileService.SaveAs(Result, _currentFilePath);
        MessageBox.Show(this, string.Format(Strings.SavedMessageFormat, _currentFilePath), "SShot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private BitmapSource Flatten()
    {
        CommitPendingTextIfAny();
        SetSelection(null);

        var size = new Size(EditorCanvas.Width, EditorCanvas.Height);
        EditorCanvas.Measure(size);
        EditorCanvas.Arrange(new Rect(size));

        var rtb = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(EditorCanvas);
        rtb.Freeze();
        return rtb;
    }

    // ---- Selection / hit-testing ----

    private AnnotationShapeBase? GetSelectedShape() =>
        _selectedShapeId is null ? null : _viewModel.Document.Shapes.FirstOrDefault(s => s.Id == _selectedShapeId);

    private void SetSelection(Guid? id)
    {
        _selectedShapeId = id;
        UpdateAdornerForSelection();
    }

    private AnnotationShapeBase? HitTest(Point point)
    {
        var shapes = _viewModel.Document.Shapes;
        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            if (Rect.Inflate(shapes[i].GetBounds(), 6, 6).Contains(point))
            {
                return shapes[i];
            }
        }

        return null;
    }

    private void UpdateAdornerForSelection()
    {
        if (_adorner is null)
        {
            return;
        }

        var shape = GetSelectedShape();
        if (shape is null)
        {
            _adorner.SetOutline(Rect.Empty);
            _adorner.ClearHandles();
            return;
        }

        _adorner.SetOutline(shape.GetBounds());

        switch (shape)
        {
            case ArrowShape arrow:
                _adorner.SetHandles(new Dictionary<string, Point>
                {
                    ["Start"] = arrow.Start,
                    ["End"] = arrow.End,
                });
                break;

            case RectangleShape or EllipseShape or HighlighterShape or MosaicShape or BlurShape:
                var b = shape.GetBounds();
                _adorner.SetHandles(
                    new Dictionary<string, Point>
                    {
                        ["TL"] = b.TopLeft,
                        ["TR"] = b.TopRight,
                        ["BL"] = b.BottomLeft,
                        ["BR"] = b.BottomRight,
                    },
                    new Dictionary<string, Cursor>
                    {
                        ["TL"] = Cursors.SizeNWSE,
                        ["BR"] = Cursors.SizeNWSE,
                        ["TR"] = Cursors.SizeNESW,
                        ["BL"] = Cursors.SizeNESW,
                    });
                break;

            default:
                _adorner.ClearHandles();
                break;
        }

        // Reflect the selected shape's own intensity into the slider, so the slider edits the
        // shape that's actually selected rather than whatever was last drawn.
        switch (shape)
        {
            case MosaicShape mosaic:
                _viewModel.MosaicBlockSize = mosaic.BlockSize;
                break;
            case BlurShape blur:
                _viewModel.BlurRadius = blur.Radius;
                break;
        }
    }

    // ---- Resize via adorner handles ----

    private void OnHandleDragDelta(object? sender, (string Key, Vector Delta) e)
    {
        var shape = GetSelectedShape();
        if (shape is null)
        {
            return;
        }

        _handleDragBefore ??= shape.Clone();
        ApplyHandleDelta(shape, e.Key, e.Delta);
        RedrawShapes();
    }

    private void OnHandleDragCompleted(object? sender, EventArgs e)
    {
        var shape = GetSelectedShape();
        var before = _handleDragBefore;
        _handleDragBefore = null;

        if (shape is null || before is null)
        {
            return;
        }

        var after = shape.Clone();
        _viewModel.UndoRedo.ExecuteAndPush(new TransformShapeCommand(shape, before, after));
    }

    private static void ApplyHandleDelta(AnnotationShapeBase shape, string key, Vector delta)
    {
        if (shape is ArrowShape arrow)
        {
            if (key == "Start")
            {
                arrow.Start += delta;
            }
            else if (key == "End")
            {
                arrow.End += delta;
            }

            return;
        }

        Rect bounds = shape.GetBounds();
        double left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom;

        switch (key)
        {
            case "TL": left += delta.X; top += delta.Y; break;
            case "TR": right += delta.X; top += delta.Y; break;
            case "BL": left += delta.X; bottom += delta.Y; break;
            case "BR": right += delta.X; bottom += delta.Y; break;
        }

        var newBounds = new Rect(
            new Point(Math.Min(left, right), Math.Min(top, bottom)),
            new Point(Math.Max(left, right), Math.Max(top, bottom)));

        switch (shape)
        {
            case RectangleShape r: r.Bounds = newBounds; break;
            case EllipseShape el: el.Bounds = newBounds; break;
            case HighlighterShape h: h.Bounds = newBounds; break;
            case MosaicShape m: m.Bounds = newBounds; break;
            case BlurShape bl: bl.Bounds = newBounds; break;
        }
    }

    // ---- Mosaic / blur intensity sliders ----

    /// <summary>
    /// Snapshots the selected shape before a slider drag, mirroring the OnHandleDragDelta/
    /// OnHandleDragCompleted pattern: the shape is mutated live per tick for immediate visual
    /// feedback, and the whole gesture becomes one undoable TransformShapeCommand at drag end.
    /// </summary>
    private void OnIntensitySliderPreviewMouseDown(object sender, MouseButtonEventArgs e) => BeginIntensityDragIfApplicable();

    private void OnIntensitySliderPreviewKeyDown(object sender, KeyEventArgs e) => BeginIntensityDragIfApplicable();

    /// <summary>
    /// Snapshots the selected shape once at the start of a mouse drag OR a keyboard-driven change
    /// (arrow/Home/End/PageUp/Down key-repeat fires many KeyDown/ValueChanged ticks per physical
    /// press) - the early return when a snapshot already exists is what coalesces either gesture
    /// into a single undo entry instead of one entry per tick.
    /// </summary>
    private void BeginIntensityDragIfApplicable()
    {
        if (_intensityDragBefore is not null)
        {
            return;
        }

        var shape = GetSelectedShape();
        if (shape is MosaicShape or BlurShape)
        {
            _intensityDragBefore = shape.Clone();
        }
    }

    private void OnIntensitySliderPreviewMouseUp(object sender, MouseButtonEventArgs e) => CommitIntensityDrag();

    private void OnIntensitySliderKeyUp(object sender, KeyEventArgs e) => CommitIntensityDrag();

    /// <summary>
    /// Also wired to LostMouseCapture: if the Slider's Thumb loses mouse capture externally
    /// (Alt-Tab, a dialog stealing focus) mid-drag, PreviewMouseUp never fires - without this,
    /// _intensityDragBefore would stay set forever, silently dropping that drag's undo entry and
    /// then also swallowing the next keyboard-driven edit (which checks the same field).
    /// </summary>
    private void OnIntensitySliderLostMouseCapture(object sender, MouseEventArgs e) => CommitIntensityDrag();

    private void CommitIntensityDrag()
    {
        var before = _intensityDragBefore;
        _intensityDragBefore = null;

        if (before is not null && GetSelectedShape() is { } shape && shape.Id == before.Id)
        {
            _viewModel.UndoRedo.ExecuteAndPush(new TransformShapeCommand(shape, before, shape.Clone()));
        }
    }

    private void OnMosaicIntensityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GetSelectedShape() is not MosaicShape mosaic)
        {
            return;
        }

        // PreviewMouseDown/Up (above) brackets a mouse-drag gesture into one TransformShapeCommand;
        // _intensityDragBefore is only set while such a drag is in progress. A Slider also raises
        // ValueChanged from keyboard input (arrow/Home/End/PageUp/Down) once focused, which never
        // goes through PreviewMouseDown/Up - without this branch that path would mutate the shape
        // directly with no Undo/Redo entry at all.
        if (_intensityDragBefore is not null)
        {
            mosaic.BlockSize = (int)e.NewValue;
            RedrawShapes();
            return;
        }

        var before = mosaic.Clone();
        mosaic.BlockSize = (int)e.NewValue;
        RedrawShapes();
        _viewModel.UndoRedo.ExecuteAndPush(new TransformShapeCommand(mosaic, before, mosaic.Clone()));
    }

    private void OnBlurIntensityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GetSelectedShape() is not BlurShape blur)
        {
            return;
        }

        if (_intensityDragBefore is not null)
        {
            blur.Radius = e.NewValue;
            RedrawShapes();
            return;
        }

        var before = blur.Clone();
        blur.Radius = e.NewValue;
        RedrawShapes();
        _viewModel.UndoRedo.ExecuteAndPush(new TransformShapeCommand(blur, before, blur.Clone()));
    }

    private void OnApplyMosaicWholeClick(object sender, RoutedEventArgs e)
    {
        var image = _viewModel.Document.BaseImage;
        var shape = new MosaicShape { Bounds = new Rect(0, 0, image.PixelWidth, image.PixelHeight), BlockSize = _viewModel.MosaicBlockSize };
        _viewModel.UndoRedo.ExecuteAndPush(new AddShapeCommand(_viewModel.Document, shape));
        SetSelection(shape.Id);
    }

    private void OnApplyBlurWholeClick(object sender, RoutedEventArgs e)
    {
        var image = _viewModel.Document.BaseImage;
        var shape = new BlurShape { Bounds = new Rect(0, 0, image.PixelWidth, image.PixelHeight), Radius = _viewModel.BlurRadius };
        _viewModel.UndoRedo.ExecuteAndPush(new AddShapeCommand(_viewModel.Document, shape));
        SetSelection(shape.Id);
    }

    // ---- Canvas mouse / keyboard ----

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        EditorCanvas.Focus();
        var point = e.GetPosition(EditorCanvas);

        switch (_viewModel.CurrentTool)
        {
            case EditorTool.Select:
                BeginSelectOrMove(point);
                break;
            case EditorTool.Text:
                BeginTextEditing(point);
                return; // don't capture the mouse - the TextBox needs normal input focus
            case EditorTool.StepStamp:
                CommitStepStamp(point);
                return;
            case EditorTool.Freehand:
                BeginFreehand(point);
                break;
            case EditorTool.Crop:
                BeginCropDrag(point);
                break;
            default:
                BeginDrawNewShape(point);
                break;
        }

        EditorCanvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(EditorCanvas);

        if (_isDraggingBody && _dragShape is not null)
        {
            var delta = point - _dragLastPoint;
            _dragShape.Translate(delta);
            _dragLastPoint = point;
            RedrawShapes();
            return;
        }

        if (_isDrawingNew)
        {
            UpdateDraftVisual(point);
            return;
        }

        if (_freehandDraft is not null)
        {
            _freehandDraft.Points.Add(point);
            UpdateFreehandGhost();
            return;
        }

        if (_isCropDragging)
        {
            UpdateCropGhost(point);
        }
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // ReleaseMouseCapture() synchronously raises LostMouseCapture below, which is what
        // actually commits a body drag / freehand draw / crop drag (see OnCanvasLostMouseCapture)
        // - this also covers capture being force-released externally (Alt-Tab, a dialog stealing
        // focus, session lock), which fires LostMouseCapture without ever reaching this handler.
        EditorCanvas.ReleaseMouseCapture();
        var point = e.GetPosition(EditorCanvas);

        if (_isDrawingNew)
        {
            FinishDrawNewShape(point);
        }
    }

    private void OnCanvasLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isDraggingBody)
        {
            _isDraggingBody = false;
            var shape = _dragShape;
            var before = _dragBeforeBody;
            _dragShape = null;
            _dragBeforeBody = null;
            if (shape is not null && before is not null)
            {
                _viewModel.UndoRedo.ExecuteAndPush(new TransformShapeCommand(shape, before, shape.Clone()));
            }

            return;
        }

        if (_freehandDraft is not null)
        {
            FinishFreehand();
            return;
        }

        if (_isCropDragging)
        {
            FinishCropDrag(e.GetPosition(EditorCanvas));
        }
    }

    private void OnCanvasKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            OnDeleteClick(sender, e);
        }
    }

    // ---- Select / move ----

    private void BeginSelectOrMove(Point point)
    {
        var hit = HitTest(point);
        SetSelection(hit?.Id);

        if (hit is not null)
        {
            _isDraggingBody = true;
            _dragShape = hit;
            _dragBeforeBody = hit.Clone();
            _dragLastPoint = point;
        }
    }

    // ---- New rect/ellipse/arrow/highlighter/mosaic/blur ----

    private void BeginDrawNewShape(Point point)
    {
        _isDrawingNew = true;
        _drawStart = point;
        _draftVisual = CreateDraftVisual(_viewModel.CurrentTool);
        if (_draftVisual is not null)
        {
            EditorCanvas.Children.Add(_draftVisual);
        }
    }

    private UIElement? CreateDraftVisual(EditorTool tool)
    {
        var color = _viewModel.SelectedColor;
        return tool switch
        {
            EditorTool.Rectangle => new Rectangle { Stroke = new SolidColorBrush(color), StrokeThickness = 3, Fill = Brushes.Transparent },
            EditorTool.Ellipse => new Ellipse { Stroke = new SolidColorBrush(color), StrokeThickness = 3, Fill = Brushes.Transparent },
            EditorTool.Highlighter => new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(120, 255, 235, 59)) },
            EditorTool.Arrow => new Line { Stroke = new SolidColorBrush(color), StrokeThickness = 3 },
            EditorTool.Mosaic => new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(160, 128, 128, 128)),
                Stroke = Brushes.DimGray,
                StrokeThickness = 1,
                StrokeDashArray = [4, 2],
            },
            EditorTool.Blur => new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(160, 160, 160, 220)),
                Stroke = Brushes.SlateBlue,
                StrokeThickness = 1,
                StrokeDashArray = [4, 2],
            },
            _ => null,
        };
    }

    private void UpdateDraftVisual(Point current)
    {
        if (_draftVisual is Line line)
        {
            line.X1 = _drawStart.X;
            line.Y1 = _drawStart.Y;
            line.X2 = current.X;
            line.Y2 = current.Y;
            return;
        }

        if (_draftVisual is FrameworkElement element)
        {
            var rect = new Rect(_drawStart, current);
            Canvas.SetLeft(element, rect.X);
            Canvas.SetTop(element, rect.Y);
            element.Width = rect.Width;
            element.Height = rect.Height;
        }
    }

    private void FinishDrawNewShape(Point point)
    {
        _isDrawingNew = false;
        if (_draftVisual is not null)
        {
            EditorCanvas.Children.Remove(_draftVisual);
            _draftVisual = null;
        }

        var rect = new Rect(_drawStart, point);
        var color = _viewModel.SelectedColor;

        AnnotationShapeBase? shape = _viewModel.CurrentTool switch
        {
            EditorTool.Rectangle when HasMinSize(rect) => new RectangleShape { Bounds = rect, StrokeColor = color },
            EditorTool.Ellipse when HasMinSize(rect) => new EllipseShape { Bounds = rect, StrokeColor = color },
            EditorTool.Highlighter when HasMinSize(rect) => new HighlighterShape { Bounds = rect },
            EditorTool.Arrow when (point - _drawStart).Length > 4 => new ArrowShape { Start = _drawStart, End = point, StrokeColor = color },
            EditorTool.Mosaic when HasMinSize(rect) => new MosaicShape { Bounds = rect, BlockSize = _viewModel.MosaicBlockSize },
            EditorTool.Blur when HasMinSize(rect) => new BlurShape { Bounds = rect, Radius = _viewModel.BlurRadius },
            _ => null,
        };

        if (shape is not null)
        {
            _viewModel.UndoRedo.ExecuteAndPush(new AddShapeCommand(_viewModel.Document, shape));
            SetSelection(shape.Id);
        }
    }

    private static bool HasMinSize(Rect rect) => rect.Width > 3 && rect.Height > 3;

    // ---- Step stamp (click, no drag) ----

    private void CommitStepStamp(Point point)
    {
        var shape = new StepStampShape { Center = point, Number = _nextStepNumber++, FillColor = _viewModel.SelectedColor };
        _viewModel.UndoRedo.ExecuteAndPush(new AddShapeCommand(_viewModel.Document, shape));
        SetSelection(shape.Id);
    }

    // ---- Freehand ----

    private void BeginFreehand(Point point)
    {
        _freehandDraft = new FreehandShape { StrokeColor = _viewModel.SelectedColor };
        _freehandDraft.Points.Add(point);
        _freehandGhost = new Polyline
        {
            Stroke = new SolidColorBrush(_viewModel.SelectedColor),
            StrokeThickness = 3,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        _freehandGhost.Points.Add(point);
        EditorCanvas.Children.Add(_freehandGhost);
    }

    private void UpdateFreehandGhost()
    {
        if (_freehandDraft is null || _freehandGhost is null)
        {
            return;
        }

        _freehandGhost.Points = new PointCollection(_freehandDraft.Points);
    }

    private void FinishFreehand()
    {
        if (_freehandGhost is not null)
        {
            EditorCanvas.Children.Remove(_freehandGhost);
            _freehandGhost = null;
        }

        var draft = _freehandDraft;
        _freehandDraft = null;

        if (draft is not null && draft.Points.Count >= 2)
        {
            _viewModel.UndoRedo.ExecuteAndPush(new AddShapeCommand(_viewModel.Document, draft));
            SetSelection(draft.Id);
        }
    }

    // ---- Text ----

    private void BeginTextEditing(Point point)
    {
        CommitPendingTextIfAny();

        _textEditorPosition = point;
        var textBox = new TextBox
        {
            MinWidth = 100,
            FontSize = 22,
            Background = Brushes.White,
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1),
        };
        Canvas.SetLeft(textBox, point.X);
        Canvas.SetTop(textBox, point.Y);
        EditorCanvas.Children.Add(textBox);
        _activeTextEditor = textBox;

        textBox.Loaded += (_, _) => textBox.Focus();
        textBox.LostFocus += (_, _) => CommitPendingTextIfAny();
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                CommitPendingTextIfAny();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelPendingText();
                e.Handled = true;
            }
        };
    }

    private void CommitPendingTextIfAny()
    {
        if (_activeTextEditor is null)
        {
            return;
        }

        string text = _activeTextEditor.Text;
        EditorCanvas.Children.Remove(_activeTextEditor);
        _activeTextEditor = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            var shape = new TextShape { Position = _textEditorPosition, Text = text, StrokeColor = _viewModel.SelectedColor };
            _viewModel.UndoRedo.ExecuteAndPush(new AddShapeCommand(_viewModel.Document, shape));
            SetSelection(shape.Id);
        }
    }

    private void CancelPendingText()
    {
        if (_activeTextEditor is null)
        {
            return;
        }

        EditorCanvas.Children.Remove(_activeTextEditor);
        _activeTextEditor = null;
    }

    // ---- Crop ----

    private void BeginCropDrag(Point point)
    {
        _isCropDragging = true;
        _cropStart = point;
        _cropGhost = new Rectangle
        {
            Stroke = Brushes.White,
            StrokeThickness = 2,
            StrokeDashArray = [4, 2],
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
        };
        EditorCanvas.Children.Add(_cropGhost);
    }

    private void UpdateCropGhost(Point current)
    {
        if (_cropGhost is null)
        {
            return;
        }

        var rect = new Rect(_cropStart, current);
        Canvas.SetLeft(_cropGhost, rect.X);
        Canvas.SetTop(_cropGhost, rect.Y);
        _cropGhost.Width = rect.Width;
        _cropGhost.Height = rect.Height;
    }

    private void FinishCropDrag(Point point)
    {
        _isCropDragging = false;
        if (_cropGhost is not null)
        {
            EditorCanvas.Children.Remove(_cropGhost);
            _cropGhost = null;
        }

        var rect = new Rect(_cropStart, point);
        if (rect.Width < 10 || rect.Height < 10)
        {
            return;
        }

        var image = _viewModel.Document.BaseImage;
        var cropRect = CropGeometry.Clamp(
            (int)Math.Round(rect.X), (int)Math.Round(rect.Y),
            (int)Math.Round(rect.Width), (int)Math.Round(rect.Height),
            image.PixelWidth, image.PixelHeight);

        if (cropRect is null)
        {
            return;
        }

        SetSelection(null);
        _viewModel.UndoRedo.ExecuteAndPush(new CropCommand(_viewModel.Document, cropRect.Value));
    }

    private void OnCropByCoordinatesClick(object sender, RoutedEventArgs e)
    {
        var image = _viewModel.Document.BaseImage;
        var dialog = new CropCoordinatesWindow(image.PixelWidth, image.PixelHeight) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultRect is { } cropRect)
        {
            SetSelection(null);
            _viewModel.UndoRedo.ExecuteAndPush(new CropCommand(_viewModel.Document, cropRect));
        }
    }
}
