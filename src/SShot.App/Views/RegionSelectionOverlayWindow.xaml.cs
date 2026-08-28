using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SShot.Core.Capture;
using SShot.Core.Capture.Dpi;

namespace SShot.App.Views;

/// <summary>
/// One instance per monitor. Shows a frozen slice of the desktop (captured before any
/// overlay appears) so the cropped pixels always match exactly what the user saw.
/// Selection state is shared across all instances via <see cref="RegionSelectionSession"/>
/// so a drag can span multiple monitors.
/// </summary>
public partial class RegionSelectionOverlayWindow : Window
{
    // Same interval WindowPickerOverlayWindow polls at: short enough that a normal key press
    // (tens of milliseconds at the very least) is still down when a tick samples it.
    private const int EscapePollIntervalMs = 40;

    private readonly Int32Rect _monitorBoundsPhysical;
    private readonly RegionSelectionSession _session;
    private readonly DispatcherTimer _escapePollTimer;

    public RegionSelectionOverlayWindow(Int32Rect monitorBoundsPhysical, BitmapSource monitorSlice, RegionSelectionSession session)
    {
        InitializeComponent();

        _monitorBoundsPhysical = monitorBoundsPhysical;
        _session = session;

        double dpiScale = DpiHelper.GetDpiScaleForMonitor(monitorBoundsPhysical);
        var dip = DpiHelper.PhysicalToDip(monitorBoundsPhysical, dpiScale);

        Left = dip.X;
        Top = dip.Y;
        Width = dip.Width;
        Height = dip.Height;

        BackgroundImage.Source = monitorSlice;
        BackgroundImage.Width = dip.Width;
        BackgroundImage.Height = dip.Height;

        _session.SelectionChanged += OnSelectionChanged;
        Loaded += OnLoaded;

        _escapePollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(EscapePollIntervalMs) };
        _escapePollTimer.Tick += OnEscapePollTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDimPath(null);
        Activate();
        Focus();
        _escapePollTimer.Start();
    }

    /// <summary>
    /// Escape is polled as well as handled in OnKeyDown (mirroring WindowPickerOverlayWindow),
    /// because these windows come one per monitor and each calls Activate()/Focus() as it loads:
    /// on a multi-monitor desktop the instance that ends up in the foreground is not necessarily
    /// the one holding WPF's keyboard focus, and OnKeyDown then fires for neither. Confirmed on a
    /// two-monitor setup, where Escape did nothing while mouse selection and Alt+F4 (dispatched by
    /// the window manager rather than by WPF input routing) both worked.
    /// </summary>
    private void OnEscapePollTick(object? sender, EventArgs e)
    {
        if (!WindowPickerSupport.IsEscapeKeyDown())
        {
            return;
        }

        // Cancel() tears down every overlay in the set, which stops the sibling timers via
        // OnClosed below; stopping this one first keeps it from cancelling twice in the meantime.
        _escapePollTimer.Stop();
        _session.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _escapePollTimer.Stop();
        base.OnClosed(e);
    }

    private double CurrentDpiScale => DpiHelper.GetDpiScale(this);

    private Point MonitorOriginPhysical => new(_monitorBoundsPhysical.X, _monitorBoundsPhysical.Y);

    private Point ToPhysicalPoint(Point localDip) =>
        DpiHelper.LocalDipToPhysical(localDip, MonitorOriginPhysical, CurrentDpiScale);

    private Point ToLocalDip(Point physicalPoint) =>
        DpiHelper.PhysicalToLocalDip(physicalPoint, MonitorOriginPhysical, CurrentDpiScale);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        _session.BeginSelection(ToPhysicalPoint(e.GetPosition(this)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_session.IsSelecting)
        {
            _session.UpdateSelection(ToPhysicalPoint(e.GetPosition(this)));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        _session.EndSelection();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _session.Cancel();
        }
    }

    private void OnSelectionChanged(object? sender, Rect physicalSelection)
    {
        UpdateDimPath(physicalSelection);
    }

    private void UpdateDimPath(Rect? physicalSelection)
    {
        var fullRect = new Rect(0, 0, Width, Height);

        if (physicalSelection is null || physicalSelection.Value.Width < 1 || physicalSelection.Value.Height < 1)
        {
            DimPath.Data = new RectangleGeometry(fullRect);
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var sel = physicalSelection.Value;
        var topLeftLocal = ToLocalDip(new Point(sel.X, sel.Y));
        var bottomRightLocal = ToLocalDip(new Point(sel.Right, sel.Bottom));
        var localSelection = new Rect(topLeftLocal, bottomRightLocal);
        var visiblePart = Rect.Intersect(localSelection, fullRect);

        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(fullRect));

        if (!visiblePart.IsEmpty)
        {
            group.Children.Add(new RectangleGeometry(visiblePart));
            SelectionBorder.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionBorder, visiblePart.X);
            Canvas.SetTop(SelectionBorder, visiblePart.Y);
            SelectionBorder.Width = visiblePart.Width;
            SelectionBorder.Height = visiblePart.Height;
        }
        else
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
        }

        DimPath.Data = group;
    }
}
