namespace SShot.Core.Annotation.Commands;

public sealed class AddShapeCommand(AnnotationDocument document, AnnotationShapeBase shape) : IEditorCommand
{
    public void Execute() => document.Shapes.Add(shape);

    public void Undo() => document.Shapes.Remove(shape);
}
