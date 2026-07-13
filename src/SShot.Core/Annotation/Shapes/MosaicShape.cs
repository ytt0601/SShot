using System.Windows;

namespace SShot.Core.Annotation.Shapes;

/// <summary>
/// Redaction shape: the App-layer renderer bakes the actual pixelated patch (via
/// SkiaMosaicRenderer, cropped from the base image at Bounds) once per redraw and shows it as
/// a plain Image visual - see CLAUDE.md for why SkiaSharp is scoped to just this filtering.
/// </summary>
public sealed class MosaicShape : AnnotationShapeBase
{
    public Rect Bounds { get; set; }

    public int BlockSize { get; set; } = 12;

    public override Rect GetBounds() => Bounds;

    public override void Translate(Vector delta) => Bounds = Rect.Offset(Bounds, delta);

    public override AnnotationShapeBase Clone() =>
        CopyBaseTo(new MosaicShape { Id = Id, Bounds = Bounds, BlockSize = BlockSize });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (MosaicShape)snapshot;
        Bounds = other.Bounds;
        BlockSize = other.BlockSize;
        RestoreBaseFrom(other);
    }
}
