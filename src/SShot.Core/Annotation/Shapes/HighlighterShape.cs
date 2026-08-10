using System.Windows.Media;

namespace SShot.Core.Annotation.Shapes;

/// <summary>Translucent band over a rectangular region, e.g. to mark up text.</summary>
public sealed class HighlighterShape : RectBoundedShape
{
    public Color FillColor { get; set; } = Color.FromArgb(120, 255, 235, 59);

    public override AnnotationShapeBase Clone() =>
        CopyRectTo(new HighlighterShape { Id = Id, FillColor = FillColor });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (HighlighterShape)snapshot;
        FillColor = other.FillColor;
        RestoreRectFrom(other);
    }
}
