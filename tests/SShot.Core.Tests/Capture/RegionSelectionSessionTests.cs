using System.Windows;
using SShot.Core.Capture;

namespace SShot.Core.Tests.Capture;

public class RegionSelectionSessionTests
{
    [Fact]
    public void CurrentRect_NormalizesAnyDragDirection()
    {
        var session = new RegionSelectionSession();

        session.BeginSelection(new Point(500, 400));
        session.UpdateSelection(new Point(100, 200));

        var rect = session.CurrentRect;

        Assert.Equal(100, rect.X);
        Assert.Equal(200, rect.Y);
        Assert.Equal(400, rect.Width);
        Assert.Equal(200, rect.Height);
    }

    [Fact]
    public void SelectionCompleted_FiresWithFinalRect_AndClearsIsSelecting()
    {
        var session = new RegionSelectionSession();
        Rect? completedRect = null;
        session.SelectionCompleted += (_, rect) => completedRect = rect;

        session.BeginSelection(new Point(0, 0));
        session.UpdateSelection(new Point(50, 60));
        session.EndSelection();

        Assert.False(session.IsSelecting);
        Assert.NotNull(completedRect);
        Assert.Equal(new Rect(0, 0, 50, 60), completedRect!.Value);
    }

    [Fact]
    public void Cancel_FiresCancelledEvent_AndDoesNotFireSelectionCompleted()
    {
        var session = new RegionSelectionSession();
        bool cancelled = false;
        bool completed = false;
        session.Cancelled += (_, _) => cancelled = true;
        session.SelectionCompleted += (_, _) => completed = true;

        session.BeginSelection(new Point(0, 0));
        session.UpdateSelection(new Point(50, 50));
        session.Cancel();

        Assert.True(cancelled);
        Assert.False(completed);
        Assert.False(session.IsSelecting);
    }

    [Fact]
    public void UpdateSelection_BeforeBeginSelection_IsIgnored()
    {
        var session = new RegionSelectionSession();
        bool changed = false;
        session.SelectionChanged += (_, _) => changed = true;

        session.UpdateSelection(new Point(10, 10));

        Assert.False(changed);
        Assert.False(session.IsSelecting);
    }
}
