using System.Windows;
using System.Windows.Media;

namespace SShot.Core.Annotation.Shapes;

/// <summary>Numbered circular stamp, e.g. for step-by-step instructions.</summary>
public sealed class StepStampShape : AnnotationShapeBase
{
    public Point Center { get; set; }

    public int Number { get; set; } = 1;

    public double Radius { get; set; } = 16;

    public Color FillColor { get; set; } = Colors.Red;

    public override Rect GetBounds() => new(Center.X - Radius, Center.Y - Radius, Radius * 2, Radius * 2);

    public override void Translate(Vector delta) => Center += delta;

    public override AnnotationShapeBase Clone() => CopyBaseTo(new StepStampShape
    {
        Id = Id,
        Center = Center,
        Number = Number,
        Radius = Radius,
        FillColor = FillColor,
    });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (StepStampShape)snapshot;
        Center = other.Center;
        Number = other.Number;
        Radius = other.Radius;
        FillColor = other.FillColor;
        RestoreBaseFrom(other);
    }
}
