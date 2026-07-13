using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SShot.Core.Annotation;
using SShot.Core.Annotation.Commands;
using SShot.Core.Annotation.Shapes;

namespace SShot.Core.Tests.Annotation;

/// <summary>
/// Reproduces the exact interleaved add/undo/undo/redo/redo sequence exercised manually while
/// verifying the editor UI, to isolate whether a discrepancy observed there was a UI-automation
/// timing/click issue or an actual bug in UndoRedoManager/AddShapeCommand.
/// </summary>
public class UndoRedoIntegrationTests
{
    private static AnnotationDocument NewDocument() =>
        new(new WriteableBitmap(700, 500, 96, 96, PixelFormats.Bgra32, null));

    [Fact]
    public void AddThree_UndoTwo_RedoOne_LeavesFirstTwo()
    {
        var document = NewDocument();
        var manager = new UndoRedoManager();
        var a = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var b = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10) };
        var c = new MosaicShape { Bounds = new Rect(20, 20, 30, 30) };

        manager.ExecuteAndPush(new AddShapeCommand(document, a));
        manager.ExecuteAndPush(new AddShapeCommand(document, b));
        manager.ExecuteAndPush(new AddShapeCommand(document, c));
        Assert.Equal([a, b, c], document.Shapes);

        manager.Undo();
        manager.Undo();
        Assert.Equal([a], document.Shapes);

        manager.Redo();
        Assert.Equal([a, b], document.Shapes);
    }

    [Fact]
    public void AddThree_UndoTwo_RedoBoth_RestoresAllThreeInOrder()
    {
        var document = NewDocument();
        var manager = new UndoRedoManager();
        var a = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var b = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10) };
        var c = new MosaicShape { Bounds = new Rect(20, 20, 30, 30) };

        manager.ExecuteAndPush(new AddShapeCommand(document, a));
        manager.ExecuteAndPush(new AddShapeCommand(document, b));
        manager.ExecuteAndPush(new AddShapeCommand(document, c));

        manager.Undo();
        manager.Undo();
        manager.Redo();
        manager.Redo();

        Assert.Equal([a, b, c], document.Shapes);
    }

    [Fact]
    public void AddThree_UndoTwo_RedoOne_ThenAddNewShape_KeepsRedoneShapeAndDropsOnlyTheOtherRedo()
    {
        // Mirrors the manual editor session: draw 3 shapes, undo 2 (removing #2 and #3), redo
        // once (restoring #2), then add unrelated new shapes (stamp, text) before ever redoing
        // #3 again. #3 (mosaic) must simply stay absent - it was never brought back, not
        // dropped by some later operation.
        var document = NewDocument();
        var manager = new UndoRedoManager();
        var rect = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var arrow = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10) };
        var mosaic = new MosaicShape { Bounds = new Rect(20, 20, 30, 30) };

        manager.ExecuteAndPush(new AddShapeCommand(document, rect));
        manager.ExecuteAndPush(new AddShapeCommand(document, arrow));
        manager.ExecuteAndPush(new AddShapeCommand(document, mosaic));

        manager.Undo(); // removes mosaic
        manager.Undo(); // removes arrow
        manager.Redo(); // restores arrow only
        Assert.Equal([rect, arrow], document.Shapes);

        var stamp = new StepStampShape { Center = new Point(50, 50), Number = 1 };
        manager.ExecuteAndPush(new AddShapeCommand(document, stamp));
        var text = new TextShape { Position = new Point(0, 0), Text = "Hello" };
        manager.ExecuteAndPush(new AddShapeCommand(document, text));

        Assert.Equal([rect, arrow, stamp, text], document.Shapes);
        Assert.DoesNotContain(mosaic, document.Shapes);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void FullManualSessionReplay_RedoMosaicAfterSave_ThenAddStampAndText_MosaicShouldBePresent()
    {
        // Exact replay of the manual UI verification session: Add rect/arrow/mosaic, undo x2,
        // redo x1 (restores arrow only, matches the saved screenshot that had no mosaic), then
        // a SECOND redo click (should restore mosaic too) before adding stamp/text/moving/deleting.
        var document = NewDocument();
        var manager = new UndoRedoManager();
        var rect = new RectangleShape { Bounds = new Rect(0, 0, 10, 10) };
        var arrow = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10) };
        var mosaic = new MosaicShape { Bounds = new Rect(20, 20, 30, 30) };

        manager.ExecuteAndPush(new AddShapeCommand(document, rect));
        manager.ExecuteAndPush(new AddShapeCommand(document, arrow));
        manager.ExecuteAndPush(new AddShapeCommand(document, mosaic));

        manager.Undo();
        manager.Undo();
        manager.Redo();
        Assert.Equal([rect, arrow], document.Shapes);
        Assert.True(manager.CanRedo, "mosaic's AddShapeCommand should still be sitting in the redo stack");

        // The extra redo click from the manual session.
        manager.Redo();
        Assert.Equal([rect, arrow, mosaic], document.Shapes);
        Assert.False(manager.CanRedo);

        var stamp = new StepStampShape { Center = new Point(50, 50), Number = 1 };
        manager.ExecuteAndPush(new AddShapeCommand(document, stamp));
        var text = new TextShape { Position = new Point(0, 0), Text = "Hello SShot" };
        manager.ExecuteAndPush(new AddShapeCommand(document, text));

        Assert.Contains(mosaic, document.Shapes);
    }
}
