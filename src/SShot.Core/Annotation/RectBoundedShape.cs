using System.Windows;

namespace SShot.Core.Annotation;

/// <summary>
/// Base for shapes whose geometry is a single axis-aligned rectangle (rectangle, ellipse,
/// highlighter, mosaic, blur). Centralizes the Bounds/GetBounds/Translate contract so
/// rect-geometry behavior (and the editor's corner-handle resize) is written once instead of
/// per shape.
/// </summary>
public abstract class RectBoundedShape : AnnotationShapeBase
{
    /// <summary>Bounding box in base-image pixel coordinates.</summary>
    public Rect Bounds { get; set; }

    public sealed override Rect GetBounds() => Bounds;

    public sealed override void Translate(Vector delta) => Bounds = Rect.Offset(Bounds, delta);

    /// <summary>Rect-shape counterpart of <see cref="AnnotationShapeBase.CopyBaseTo{T}"/>:
    /// copies Bounds plus the inherited base properties.</summary>
    protected T CopyRectTo<T>(T target) where T : RectBoundedShape
    {
        target.Bounds = Bounds;
        return CopyBaseTo(target);
    }

    /// <summary>Rect-shape counterpart of <see cref="AnnotationShapeBase.RestoreBaseFrom"/>.</summary>
    protected void RestoreRectFrom(RectBoundedShape snapshot)
    {
        Bounds = snapshot.Bounds;
        RestoreBaseFrom(snapshot);
    }
}
