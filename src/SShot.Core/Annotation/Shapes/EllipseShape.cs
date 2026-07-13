using System.Windows;

namespace SShot.Core.Annotation.Shapes;

public sealed class EllipseShape : AnnotationShapeBase
{
    public Rect Bounds { get; set; }

    public override Rect GetBounds() => Bounds;

    public override void Translate(Vector delta) => Bounds = Rect.Offset(Bounds, delta);

    public override AnnotationShapeBase Clone() => CopyBaseTo(new EllipseShape { Id = Id, Bounds = Bounds });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (EllipseShape)snapshot;
        Bounds = other.Bounds;
        RestoreBaseFrom(other);
    }
}
