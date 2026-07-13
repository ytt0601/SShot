using System.Windows;
using System.Windows.Media;

namespace SShot.Core.Annotation;

/// <summary>
/// Base for every annotation placed on a capture. Deliberately a plain model (no
/// INotifyPropertyChanged/ViewModel machinery) - the editor re-renders the whole canvas from
/// the document's shape list whenever UndoRedoManager reports a state change, so shapes don't
/// need to notify anyone of changes themselves.
/// </summary>
public abstract class AnnotationShapeBase
{
    /// <summary>
    /// Stable across Clone() (a snapshot of the same logical shape at a different point in
    /// time, e.g. before/after a transform) - lets the editor re-find "the currently selected
    /// shape" by Id after a redraw.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    public Color StrokeColor { get; set; } = Colors.Red;

    public double StrokeThickness { get; set; } = 3;

    /// <summary>Bounding box in base-image pixel coordinates.</summary>
    public abstract Rect GetBounds();

    public abstract void Translate(Vector delta);

    public abstract AnnotationShapeBase Clone();

    /// <summary>
    /// Copies every mutable property from a snapshot (same Id, same concrete type - typically
    /// produced by this instance's own Clone() earlier) back into this instance. Used by
    /// TransformShapeCommand to implement undo/redo as in-place restores rather than swapping
    /// object references in the document's shape list.
    /// </summary>
    public abstract void RestoreFrom(AnnotationShapeBase snapshot);

    protected T CopyBaseTo<T>(T target) where T : AnnotationShapeBase
    {
        target.StrokeColor = StrokeColor;
        target.StrokeThickness = StrokeThickness;
        return target;
    }

    protected void RestoreBaseFrom(AnnotationShapeBase snapshot)
    {
        StrokeColor = snapshot.StrokeColor;
        StrokeThickness = snapshot.StrokeThickness;
    }
}
