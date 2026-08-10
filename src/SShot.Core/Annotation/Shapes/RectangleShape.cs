namespace SShot.Core.Annotation.Shapes;

public sealed class RectangleShape : RectBoundedShape
{
    public override AnnotationShapeBase Clone() => CopyRectTo(new RectangleShape { Id = Id });

    public override void RestoreFrom(AnnotationShapeBase snapshot) => RestoreRectFrom((RectangleShape)snapshot);
}
