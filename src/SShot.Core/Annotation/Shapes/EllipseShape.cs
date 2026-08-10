namespace SShot.Core.Annotation.Shapes;

public sealed class EllipseShape : RectBoundedShape
{
    public override AnnotationShapeBase Clone() => CopyRectTo(new EllipseShape { Id = Id });

    public override void RestoreFrom(AnnotationShapeBase snapshot) => RestoreRectFrom((EllipseShape)snapshot);
}
