using System.Windows;

namespace SShot.Core.Capture;

/// <summary>
/// Pure geometry state machine for a drag-to-select region, shared by all per-monitor
/// overlay windows so a selection can span multiple monitors. All points/rects are in
/// physical-pixel, virtual-desktop-relative coordinates. No WPF Window dependency.
/// </summary>
public sealed class RegionSelectionSession
{
    private Point _anchor;
    private Point _current;

    public bool IsSelecting { get; private set; }

    public event EventHandler<Rect>? SelectionChanged;

    public event EventHandler<Rect>? SelectionCompleted;

    public event EventHandler? Cancelled;

    public Rect CurrentRect => new(
        Math.Min(_anchor.X, _current.X),
        Math.Min(_anchor.Y, _current.Y),
        Math.Abs(_current.X - _anchor.X),
        Math.Abs(_current.Y - _anchor.Y));

    public void BeginSelection(Point physicalPoint)
    {
        _anchor = physicalPoint;
        _current = physicalPoint;
        IsSelecting = true;
        SelectionChanged?.Invoke(this, CurrentRect);
    }

    public void UpdateSelection(Point physicalPoint)
    {
        if (!IsSelecting)
        {
            return;
        }

        _current = physicalPoint;
        SelectionChanged?.Invoke(this, CurrentRect);
    }

    public void EndSelection()
    {
        if (!IsSelecting)
        {
            return;
        }

        IsSelecting = false;
        SelectionCompleted?.Invoke(this, CurrentRect);
    }

    public void Cancel()
    {
        IsSelecting = false;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
