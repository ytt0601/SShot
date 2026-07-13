using System.Windows;
using System.Windows.Media;

namespace SShot.App.Rendering;

internal static class ArrowGeometryBuilder
{
    public static Geometry Build(Point start, Point end, double strokeThickness)
    {
        var group = new GeometryGroup();
        group.Children.Add(new LineGeometry(start, end));

        var direction = end - start;
        if (direction.Length < 0.001)
        {
            return group;
        }

        direction.Normalize();
        double headLength = Math.Max(10, strokeThickness * 4);
        double headWidth = Math.Max(8, strokeThickness * 3);

        var back = end - (direction * headLength);
        var normal = new Vector(-direction.Y, direction.X);
        var left = back + (normal * headWidth / 2);
        var right = back - (normal * headWidth / 2);

        var headFigure = new PathFigure { StartPoint = end, IsClosed = true, IsFilled = true };
        headFigure.Segments.Add(new LineSegment(left, true));
        headFigure.Segments.Add(new LineSegment(right, true));
        group.Children.Add(new PathGeometry(new[] { headFigure }));

        return group;
    }
}
