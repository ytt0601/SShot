using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SShot.Core.Annotation;
using SShot.Core.Annotation.Commands;
using SShot.Core.Annotation.Shapes;

namespace SShot.Core.Tests.Annotation;

public class AnnotationCommandsTests
{
    private static AnnotationDocument NewDocument(int width = 100, int height = 100) =>
        new(new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null));

    [Fact]
    public void AddShapeCommand_Execute_AddsToDocument_Undo_Removes()
    {
        var document = NewDocument();
        var shape = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var command = new AddShapeCommand(document, shape);

        command.Execute();
        Assert.Single(document.Shapes);

        command.Undo();
        Assert.Empty(document.Shapes);
    }

    [Fact]
    public void DeleteShapeCommand_Undo_ReinsertsAtOriginalIndex()
    {
        var document = NewDocument();
        var first = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var second = new RectangleShape { Bounds = new Rect(20, 20, 10, 10) };
        document.Shapes.Add(first);
        document.Shapes.Add(second);

        var command = new DeleteShapeCommand(document, first);
        command.Execute();
        Assert.Equal([second], document.Shapes);

        command.Undo();
        Assert.Equal([first, second], document.Shapes);
    }

    [Fact]
    public void TransformShapeCommand_MutatesShapeInPlace_NotReferenceSwap()
    {
        var document = NewDocument();
        var shape = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        document.Shapes.Add(shape);

        var before = shape.Clone();
        shape.Bounds = new Rect(50, 50, 10, 10);
        var after = shape.Clone();

        var command = new TransformShapeCommand(shape, before, after);

        command.Undo();
        Assert.Equal(new Rect(0, 0, 10, 10), shape.Bounds);
        Assert.Same(shape, document.Shapes[0]);

        command.Execute();
        Assert.Equal(new Rect(50, 50, 10, 10), shape.Bounds);
        Assert.Same(shape, document.Shapes[0]);
    }

    [Fact]
    public void ReorderShapeCommand_MovesToFront_AndUndoRestoresOriginalOrder()
    {
        var document = NewDocument();
        var a = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var b = new RectangleShape { Bounds = new Rect(10, 10, 10, 10) };
        var c = new RectangleShape { Bounds = new Rect(20, 20, 10, 10) };
        document.Shapes.AddRange([a, b, c]);

        var command = new ReorderShapeCommand(document, a, 2);
        command.Execute();
        Assert.Equal([b, c, a], document.Shapes);

        command.Undo();
        Assert.Equal([a, b, c], document.Shapes);
    }

    [Fact]
    public void CropCommand_ReplacesImage_AndTranslatesShapes_UndoRestoresBoth()
    {
        var document = NewDocument(200, 200);
        var originalImage = document.BaseImage;
        var shape = new RectangleShape { Bounds = new Rect(60, 60, 10, 10) };
        document.Shapes.Add(shape);

        var cropRect = new Int32Rect(50, 50, 80, 80);
        var command = new CropCommand(document, cropRect);

        command.Execute();
        Assert.Equal(80, document.BaseImage.PixelWidth);
        Assert.Equal(new Rect(10, 10, 10, 10), shape.Bounds);

        command.Undo();
        Assert.Same(originalImage, document.BaseImage);
        Assert.Equal(new Rect(60, 60, 10, 10), shape.Bounds);
    }
}
