using System.Windows;

namespace SShot.Core.Annotation.Shapes;

public sealed class FreehandShape : AnnotationShapeBase
{
    public List<Point> Points { get; set; } = new();

    public override Rect GetBounds()
    {
        if (Points.Count == 0)
        {
            return Rect.Empty;
        }

        double minX = Points.Min(p => p.X);
        double minY = Points.Min(p => p.Y);
        double maxX = Points.Max(p => p.X);
        double maxY = Points.Max(p => p.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    public override void Translate(Vector delta)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            Points[i] += delta;
        }
    }

    public override AnnotationShapeBase Clone() =>
        CopyBaseTo(new FreehandShape { Id = Id, Points = new List<Point>(Points) });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (FreehandShape)snapshot;
        Points = new List<Point>(other.Points);
        RestoreBaseFrom(other);
    }
}
