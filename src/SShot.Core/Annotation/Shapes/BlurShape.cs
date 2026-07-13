using System.Windows;

namespace SShot.Core.Annotation.Shapes;

/// <summary>Redaction shape rendered via SkiaBlurRenderer - see MosaicShape for the rationale.</summary>
public sealed class BlurShape : AnnotationShapeBase
{
    public Rect Bounds { get; set; }

    public double Radius { get; set; } = 10;

    public override Rect GetBounds() => Bounds;

    public override void Translate(Vector delta) => Bounds = Rect.Offset(Bounds, delta);

    public override AnnotationShapeBase Clone() =>
        CopyBaseTo(new BlurShape { Id = Id, Bounds = Bounds, Radius = Radius });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (BlurShape)snapshot;
        Bounds = other.Bounds;
        Radius = other.Radius;
        RestoreBaseFrom(other);
    }
}
