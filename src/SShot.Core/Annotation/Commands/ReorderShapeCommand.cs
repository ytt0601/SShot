namespace SShot.Core.Annotation.Commands;

public sealed class ReorderShapeCommand(AnnotationDocument document, AnnotationShapeBase shape, int newIndex) : IEditorCommand
{
    private int _oldIndex;

    public void Execute()
    {
        _oldIndex = document.Shapes.IndexOf(shape);
        if (_oldIndex < 0)
        {
            return;
        }

        int target = Math.Clamp(newIndex, 0, document.Shapes.Count - 1);
        Move(_oldIndex, target);
    }

    public void Undo()
    {
        int currentIndex = document.Shapes.IndexOf(shape);
        if (currentIndex < 0)
        {
            return;
        }

        Move(currentIndex, _oldIndex);
    }

    private void Move(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
        {
            return;
        }

        var item = document.Shapes[oldIndex];
        document.Shapes.RemoveAt(oldIndex);
        document.Shapes.Insert(newIndex, item);
    }
}
