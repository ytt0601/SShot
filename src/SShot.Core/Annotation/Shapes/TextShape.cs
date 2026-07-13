using System.Windows;
using System.Windows.Media;

namespace SShot.Core.Annotation.Shapes;

public sealed class TextShape : AnnotationShapeBase
{
    public Point Position { get; set; }

    public string Text { get; set; } = string.Empty;

    public double FontSize { get; set; } = 22;

    /// <summary>
    /// Measured bounds, set by the renderer once actual text layout is known (FormattedText
    /// measurement is a rendering concern - the App layer's ShapeVisualFactory updates this
    /// after creating the visual so hit-testing/selection has an accurate box).
    /// </summary>
    public Size MeasuredSize { get; set; } = new(120, 30);

    public override Rect GetBounds() => new(Position, MeasuredSize);

    public override void Translate(Vector delta) => Position += delta;

    public override AnnotationShapeBase Clone() => CopyBaseTo(new TextShape
    {
        Id = Id,
        Position = Position,
        Text = Text,
        FontSize = FontSize,
        MeasuredSize = MeasuredSize,
    });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (TextShape)snapshot;
        Position = other.Position;
        Text = other.Text;
        FontSize = other.FontSize;
        MeasuredSize = other.MeasuredSize;
        RestoreBaseFrom(other);
    }
}
