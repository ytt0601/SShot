namespace SShot.Core.Annotation.Commands;

/// <summary>
/// Represents any in-place edit to a shape (move/resize/recolor/retype): restores property
/// values into the SAME shape instance from a before/after snapshot, rather than swapping
/// object references in the document's shape list. This lets the view mutate the live shape
/// directly during an interactive drag (for immediate visual feedback, redrawing per frame)
/// and only wrap the whole gesture into one undoable command once the drag ends.
/// </summary>
public sealed class TransformShapeCommand(AnnotationShapeBase shape, AnnotationShapeBase before, AnnotationShapeBase after) : IEditorCommand
{
    public void Execute() => shape.RestoreFrom(after);

    public void Undo() => shape.RestoreFrom(before);
}
