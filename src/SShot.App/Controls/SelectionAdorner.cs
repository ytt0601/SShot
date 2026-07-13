using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SShot.App.Controls;

/// <summary>
/// A single instance is attached to the editor's root Canvas (not per-shape). Because the
/// adorned element is the Canvas itself, this adorner's local coordinate space is identical to
/// the canvas's own (absolute, base-image-pixel) coordinate space - so outline/handle positions
/// are just the selected shape's own Bounds/Start/End/Center values, no per-visual coordinate
/// translation needed.
/// </summary>
public sealed class SelectionAdorner : Adorner
{
    private readonly VisualCollection _visualChildren;
    private readonly Dictionary<string, Thumb> _handles = new();
    private IReadOnlyDictionary<string, Point> _handlePositions = new Dictionary<string, Point>();
    private Rect _outline = Rect.Empty;

    public event EventHandler<(string Key, Vector Delta)>? HandleDragDelta;

    public event EventHandler? HandleDragCompleted;

    public SelectionAdorner(UIElement adornedCanvas)
        : base(adornedCanvas)
    {
        _visualChildren = new VisualCollection(this);
        IsHitTestVisible = true;
    }

    public void SetOutline(Rect outline)
    {
        _outline = outline;
        InvalidateVisual();
    }

    public void SetHandles(IReadOnlyDictionary<string, Point> positions, IReadOnlyDictionary<string, Cursor>? cursors = null)
    {
        foreach (string staleKey in _handles.Keys.Where(k => !positions.ContainsKey(k)).ToList())
        {
            _visualChildren.Remove(_handles[staleKey]);
            _handles.Remove(staleKey);
        }

        foreach (string key in positions.Keys)
        {
            if (_handles.ContainsKey(key))
            {
                continue;
            }

            string capturedKey = key;
            var thumb = new Thumb
            {
                Width = 9,
                Height = 9,
                Background = Brushes.White,
                BorderBrush = Brushes.DeepSkyBlue,
                BorderThickness = new Thickness(1.5),
                Cursor = cursors is not null && cursors.TryGetValue(key, out var cursor) ? cursor : Cursors.SizeAll,
            };
            thumb.DragDelta += (_, e) => HandleDragDelta?.Invoke(this, (capturedKey, new Vector(e.HorizontalChange, e.VerticalChange)));
            thumb.DragCompleted += (_, _) => HandleDragCompleted?.Invoke(this, EventArgs.Empty);
            _handles[key] = thumb;
            _visualChildren.Add(thumb);
        }

        _handlePositions = positions;
        InvalidateArrange();
    }

    public void ClearHandles() => SetHandles(new Dictionary<string, Point>());

    protected override int VisualChildrenCount => _visualChildren.Count;

    protected override Visual GetVisualChild(int index) => _visualChildren[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (key, thumb) in _handles)
        {
            var rect = _handlePositions.TryGetValue(key, out var pos)
                ? new Rect(pos.X - 4.5, pos.Y - 4.5, 9, 9)
                : new Rect(-100, -100, 9, 9);
            thumb.Arrange(rect);
        }

        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_outline.IsEmpty)
        {
            return;
        }

        var pen = new Pen(Brushes.DeepSkyBlue, 1.5) { DashStyle = new DashStyle([4, 2], 0) };
        drawingContext.DrawRectangle(null, pen, _outline);
    }
}
