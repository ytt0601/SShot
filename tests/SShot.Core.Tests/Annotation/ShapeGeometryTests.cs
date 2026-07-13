using System.Windows;
using SShot.Core.Annotation.Shapes;

namespace SShot.Core.Tests.Annotation;

public class ShapeGeometryTests
{
    [Fact]
    public void ArrowShape_GetBounds_IsRectBetweenStartAndEnd()
    {
        var arrow = new ArrowShape { Start = new Point(50, 10), End = new Point(10, 50) };

        Assert.Equal(new Rect(new Point(50, 10), new Point(10, 50)), arrow.GetBounds());
    }

    [Fact]
    public void ArrowShape_Translate_MovesBothEndpoints()
    {
        var arrow = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10) };

        arrow.Translate(new Vector(5, -5));

        Assert.Equal(new Point(5, -5), arrow.Start);
        Assert.Equal(new Point(15, 5), arrow.End);
    }

    [Fact]
    public void FreehandShape_GetBounds_IsBoundingBoxOfAllPoints()
    {
        var freehand = new FreehandShape
        {
            Points = [new Point(10, 40), new Point(30, 5), new Point(50, 20)],
        };

        Assert.Equal(new Rect(10, 5, 40, 35), freehand.GetBounds());
    }

    [Fact]
    public void FreehandShape_GetBounds_EmptyWhenNoPoints()
    {
        var freehand = new FreehandShape();

        Assert.Equal(Rect.Empty, freehand.GetBounds());
    }

    [Fact]
    public void StepStampShape_GetBounds_IsSquareAroundCenter()
    {
        var stamp = new StepStampShape { Center = new Point(100, 100), Radius = 16 };

        Assert.Equal(new Rect(84, 84, 32, 32), stamp.GetBounds());
    }

    [Fact]
    public void Clone_PreservesId_ForSelectionTrackingAcrossUndoRedo()
    {
        var original = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var clone = original.Clone();

        Assert.Equal(original.Id, clone.Id);
        Assert.NotSame(original, clone);
    }
}
