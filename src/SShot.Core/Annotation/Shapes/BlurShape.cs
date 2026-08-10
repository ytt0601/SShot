namespace SShot.Core.Annotation.Shapes;

/// <summary>Redaction shape rendered via SkiaBlurRenderer - see MosaicShape for the rationale.</summary>
public sealed class BlurShape : RectBoundedShape
{
    public double Radius { get; set; } = 10;

    public override AnnotationShapeBase Clone() =>
        CopyRectTo(new BlurShape { Id = Id, Radius = Radius });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (BlurShape)snapshot;
        Radius = other.Radius;
        RestoreRectFrom(other);
    }
}
