using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SShot.Core.Annotation;

namespace SShot.App.ViewModels;

public partial class EditorViewModel : ObservableObject
{
    public static readonly Color[] Palette =
    [
        Colors.Red, Colors.OrangeRed, Colors.Gold, Colors.LimeGreen, Colors.DeepSkyBlue, Colors.Black, Colors.White,
    ];

    public AnnotationDocument Document { get; }

    public UndoRedoManager UndoRedo { get; } = new();

    [ObservableProperty]
    private EditorTool _currentTool = EditorTool.Select;

    [ObservableProperty]
    private Color _selectedColor = Colors.Red;

    /// <summary>Mosaic block size in pixels - shared by the "draw a new mosaic" default and the
    /// intensity slider for whichever mosaic shape is currently selected.</summary>
    [ObservableProperty]
    private int _mosaicBlockSize = 12;

    /// <summary>Blur radius in pixels - shared the same way as <see cref="MosaicBlockSize"/>.</summary>
    [ObservableProperty]
    private double _blurRadius = 10;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    public EditorViewModel(AnnotationDocument document)
    {
        Document = document;
        UndoRedo.StateChanged += (_, _) =>
        {
            CanUndo = UndoRedo.CanUndo;
            CanRedo = UndoRedo.CanRedo;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRedo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => UndoRedo.Redo();
}
