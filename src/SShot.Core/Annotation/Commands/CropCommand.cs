using System.Windows;
using System.Windows.Media.Imaging;

namespace SShot.Core.Annotation.Commands;

/// <summary>
/// cropRect is in the current base image's own pixel coordinates. Shapes are translated so
/// they stay visually aligned to the new, smaller canvas; shapes that end up outside the new
/// bounds are left as-is (simply off-canvas) rather than clipped/deleted - undo restores them
/// to their original, visible position.
/// </summary>
public sealed class CropCommand(AnnotationDocument document, Int32Rect cropRect) : IEditorCommand
{
    private BitmapSource? _previousImage;

    public void Execute()
    {
        _previousImage = document.BaseImage;

        var cropped = new CroppedBitmap(document.BaseImage, cropRect);
        cropped.Freeze();
        document.SetImage(cropped);

        var delta = new Vector(-cropRect.X, -cropRect.Y);
        foreach (var shape in document.Shapes)
        {
            shape.Translate(delta);
        }
    }

    public void Undo()
    {
        if (_previousImage is null)
        {
            return;
        }

        document.SetImage(_previousImage);

        var delta = new Vector(cropRect.X, cropRect.Y);
        foreach (var shape in document.Shapes)
        {
            shape.Translate(delta);
        }
    }
}
