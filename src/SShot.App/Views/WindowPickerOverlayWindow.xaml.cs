using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SShot.App.Resources;
using SShot.Core.Capture;
using SShot.Core.Capture.Dpi;

namespace SShot.App.Views;

/// <summary>
/// Click-through (WS_EX_TRANSPARENT | WS_EX_LAYERED) overlay spanning the virtual desktop.
/// Because it's click-through, hover detection and click-to-confirm are done by polling
/// (GetCursorPos/WindowFromPoint/GetAsyncKeyState) on a DispatcherTimer rather than via
/// WPF mouse events, which a click-through window never receives.
///
/// By default the whole top-level window under the cursor is targeted (unchanged
/// behavior). Holding Ctrl switches to "child mode": the exact window under the
/// cursor is targeted instead, which may be a smaller pane/control living inside
/// the app (e.g. a file-tree or list pane with its own HWND) rather than the app
/// as a whole. Releasing Ctrl goes back to whole-app targeting.
/// </summary>
public partial class WindowPickerOverlayWindow : Window
{
    private static readonly Brush RootModeStroke = Brushes.DeepSkyBlue;
    private static readonly Brush RootModeFill = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0xD8, 0xFF));
    private static readonly Brush ChildModeStroke = Brushes.Orange;
    private static readonly Brush ChildModeFill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xA5, 0x00));

    private readonly DispatcherTimer _pollTimer;
    private readonly Int32Rect _virtualBoundsPhysical;
    private double _dpiScale;
    private IntPtr _ownHwnd = IntPtr.Zero;
    private IntPtr _hoveredHwnd = IntPtr.Zero;
    private bool _hoveredIsChildMode;
    private bool _wasLeftButtonDown;

    public event EventHandler<IntPtr>? WindowConfirmed;

    public event EventHandler? Cancelled;

    public WindowPickerOverlayWindow()
    {
        InitializeComponent();

        _virtualBoundsPhysical = VirtualScreenBounds.GetVirtualDesktopBounds();
        _dpiScale = DpiHelper.GetDpiScaleForMonitor(_virtualBoundsPhysical);
        var dip = DpiHelper.PhysicalToDip(_virtualBoundsPhysical, _dpiScale);

        Left = dip.X;
        Top = dip.Y;
        Width = dip.Width;
        Height = dip.Height;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
        };
        KeyDown += OnKeyDown;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _pollTimer.Tick += OnPollTick;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
        _ownHwnd = hwndSource.Handle;

        WindowPickerSupport.MakeClickThrough(_ownHwnd);

        // The constructor's DIP placement used the DPI of whichever monitor MonitorFromRect
        // picked for the virtual bounds; the DPI Windows actually assigned this HWND can differ
        // on mixed-DPI setups, which would both offset the window and misalign every highlight.
        // Pin the window to the physical virtual-desktop bounds and adopt its real scale.
        WindowPickerSupport.SetPhysicalBounds(_ownHwnd, _virtualBoundsPhysical);
        _dpiScale = DpiHelper.GetDpiScale(this);

        _pollTimer.Start();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _dpiScale = newDpi.DpiScaleX;
        if (_ownHwnd != IntPtr.Zero)
        {
            WindowPickerSupport.SetPhysicalBounds(_ownHwnd, _virtualBoundsPhysical);
        }

        if (_hoveredHwnd != IntPtr.Zero)
        {
            UpdateHighlight(_hoveredHwnd, _hoveredIsChildMode);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _pollTimer.Stop();
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        bool childMode = WindowPickerSupport.IsCtrlKeyDown();
        IntPtr candidate = childMode ? WindowPickerSupport.GetWindowAtCursor() : WindowPickerSupport.GetRootWindowAtCursor();

        if (candidate != IntPtr.Zero && candidate != _ownHwnd
            && (candidate != _hoveredHwnd || childMode != _hoveredIsChildMode))
        {
            _hoveredHwnd = candidate;
            _hoveredIsChildMode = childMode;
            UpdateHighlight(_hoveredHwnd, _hoveredIsChildMode);
        }

        bool leftDown = WindowPickerSupport.IsLeftMouseButtonDown();
        if (leftDown && !_wasLeftButtonDown && _hoveredHwnd != IntPtr.Zero)
        {
            _pollTimer.Stop();
            WindowConfirmed?.Invoke(this, _hoveredHwnd);
        }

        _wasLeftButtonDown = leftDown;
    }

    private void UpdateHighlight(IntPtr hwnd, bool childMode)
    {
        var bounds = WindowCaptureService.TryGetWindowBounds(hwnd);
        if (bounds is null)
        {
            HighlightBorder.Visibility = Visibility.Collapsed;
            HintBorder.Visibility = Visibility.Collapsed;
            return;
        }

        // _dpiScale is the window's actual assigned DPI (set in OnSourceInitialized / OnDpiChanged
        // after the HWND is pinned to physical bounds), so dividing by it maps physical pixels to
        // this window's DIP space uniformly across all monitors it spans.
        var local = DpiHelper.PhysicalToLocalDip(
            bounds.Value, new Point(_virtualBoundsPhysical.X, _virtualBoundsPhysical.Y), _dpiScale);
        double localX = local.X;
        double localY = local.Y;
        double localWidth = local.Width;
        double localHeight = local.Height;

        Canvas.SetLeft(HighlightBorder, localX);
        Canvas.SetTop(HighlightBorder, localY);
        HighlightBorder.Width = localWidth;
        HighlightBorder.Height = localHeight;
        HighlightBorder.Stroke = childMode ? ChildModeStroke : RootModeStroke;
        HighlightBorder.Fill = childMode ? ChildModeFill : RootModeFill;
        HighlightBorder.Visibility = Visibility.Visible;

        HintText.Text = childMode ? Strings.WindowPickerHintChildMode : Strings.WindowPickerHintDefault;
        HintBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double hintY = localY - HintBorder.DesiredSize.Height - 4;
        if (hintY < 0)
        {
            hintY = localY + localHeight + 4;
        }

        Canvas.SetLeft(HintBorder, localX);
        Canvas.SetTop(HintBorder, hintY);
        HintBorder.Visibility = Visibility.Visible;
    }
}
