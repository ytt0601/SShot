namespace SShot.Core.Annotation.Commands;

public interface IEditorCommand
{
    void Execute();

    void Undo();
}
