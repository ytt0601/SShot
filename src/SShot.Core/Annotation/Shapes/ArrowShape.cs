using System.Windows;

namespace SShot.Core.Annotation.Shapes;

public sealed class ArrowShape : AnnotationShapeBase
{
    public Point Start { get; set; }

    public Point End { get; set; }

    public override Rect GetBounds() => new(Start, End);

    public override void Translate(Vector delta)
    {
        Start += delta;
        End += delta;
    }

    public override AnnotationShapeBase Clone() => CopyBaseTo(new ArrowShape { Id = Id, Start = Start, End = End });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (ArrowShape)snapshot;
        Start = other.Start;
        End = other.End;
        RestoreBaseFrom(other);
    }
}
