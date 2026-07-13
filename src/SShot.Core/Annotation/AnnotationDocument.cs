using System.Windows.Media.Imaging;

namespace SShot.Core.Annotation;

/// <summary>
/// The image being annotated plus its shape list. Shapes are ordered back-to-front (index 0
/// is drawn first / sits behind everything else); the editor's ZIndex mirrors this order.
/// A plain List (not ObservableCollection) is enough: the editor doesn't rely on collection
/// change notifications to redraw - it redraws in full whenever UndoRedoManager reports a
/// state change (see EditorWindow), since commands can mutate shapes in place (see
/// TransformShapeCommand/AnnotationShapeBase.RestoreFrom).
/// </summary>
public sealed class AnnotationDocument(BitmapSource baseImage)
{
    public BitmapSource BaseImage { get; private set; } = baseImage;

    public List<AnnotationShapeBase> Shapes { get; } = new();

    /// <summary>Used only by CropCommand - replaces the image being annotated in place.</summary>
    public void SetImage(BitmapSource newImage) => BaseImage = newImage;
}
