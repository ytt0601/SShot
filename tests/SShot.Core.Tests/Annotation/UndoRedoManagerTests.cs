using SShot.Core.Annotation;
using SShot.Core.Annotation.Commands;

namespace SShot.Core.Tests.Annotation;

file sealed class RecordingCommand : IEditorCommand
{
    public int ExecuteCount { get; private set; }

    public int UndoCount { get; private set; }

    public void Execute() => ExecuteCount++;

    public void Undo() => UndoCount++;
}

public class UndoRedoManagerTests
{
    [Fact]
    public void ExecuteAndPush_RunsCommandAndEnablesUndo()
    {
        var manager = new UndoRedoManager();
        var command = new RecordingCommand();

        manager.ExecuteAndPush(command);

        Assert.Equal(1, command.ExecuteCount);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Undo_ThenRedo_RoundTrips()
    {
        var manager = new UndoRedoManager();
        var command = new RecordingCommand();
        manager.ExecuteAndPush(command);

        manager.Undo();
        Assert.Equal(1, command.UndoCount);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);

        manager.Redo();
        Assert.Equal(2, command.ExecuteCount);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void ExecuteAndPush_AfterUndo_ClearsRedoStack()
    {
        var manager = new UndoRedoManager();
        var first = new RecordingCommand();
        var second = new RecordingCommand();

        manager.ExecuteAndPush(first);
        manager.Undo();
        Assert.True(manager.CanRedo);

        manager.ExecuteAndPush(second);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Undo_OnEmptyStack_IsNoOp()
    {
        var manager = new UndoRedoManager();
        manager.Undo();
        Assert.False(manager.CanUndo);
    }

    [Fact]
    public void StateChanged_FiresOnEveryOperation()
    {
        var manager = new UndoRedoManager();
        int fireCount = 0;
        manager.StateChanged += (_, _) => fireCount++;

        manager.ExecuteAndPush(new RecordingCommand());
        manager.Undo();
        manager.Redo();
        manager.Clear();

        Assert.Equal(4, fireCount);
    }
}
