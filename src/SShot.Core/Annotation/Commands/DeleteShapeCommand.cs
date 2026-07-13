namespace SShot.Core.Annotation.Commands;

public sealed class DeleteShapeCommand(AnnotationDocument document, AnnotationShapeBase shape) : IEditorCommand
{
    private int _index;

    public void Execute()
    {
        _index = document.Shapes.IndexOf(shape);
        document.Shapes.Remove(shape);
    }

    public void Undo() => document.Shapes.Insert(Math.Clamp(_index, 0, document.Shapes.Count), shape);
}
