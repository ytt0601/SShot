using System.Windows;
using System.Windows.Media;

namespace SShot.Core.Annotation.Shapes;

/// <summary>Translucent band over a rectangular region, e.g. to mark up text.</summary>
public sealed class HighlighterShape : AnnotationShapeBase
{
    public Rect Bounds { get; set; }

    public Color FillColor { get; set; } = Color.FromArgb(120, 255, 235, 59);

    public override Rect GetBounds() => Bounds;

    public override void Translate(Vector delta) => Bounds = Rect.Offset(Bounds, delta);

    public override AnnotationShapeBase Clone() =>
        CopyBaseTo(new HighlighterShape { Id = Id, Bounds = Bounds, FillColor = FillColor });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (HighlighterShape)snapshot;
        Bounds = other.Bounds;
        FillColor = other.FillColor;
        RestoreBaseFrom(other);
    }
}
