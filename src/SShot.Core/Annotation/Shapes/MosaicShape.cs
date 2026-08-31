namespace SShot.Core.Annotation.Shapes;

/// <summary>
/// Redaction shape: the App-layer renderer bakes the actual pixelated patch (via
/// SkiaMosaicRenderer, cropped from the base image at Bounds) once per redraw and shows it as
/// a plain Image visual. SkiaSharp is deliberately scoped to just this filtering work.
/// </summary>
public sealed class MosaicShape : RectBoundedShape
{
    public int BlockSize { get; set; } = 12;

    public override AnnotationShapeBase Clone() =>
        CopyRectTo(new MosaicShape { Id = Id, BlockSize = BlockSize });

    public override void RestoreFrom(AnnotationShapeBase snapshot)
    {
        var other = (MosaicShape)snapshot;
        BlockSize = other.BlockSize;
        RestoreRectFrom(other);
    }
}
