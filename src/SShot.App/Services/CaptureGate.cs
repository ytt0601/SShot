namespace SShot.App.Services;

/// <summary>
/// Single shared gate so hotkeys and window buttons can't trigger two overlapping captures.
/// RunHiddenAsync/RunHiddenIfVisibleAsync (MainWindow, FloatingToolbarWindow, GlobalHotkeyManager)
/// call IAsyncRelayCommand.ExecuteAsync directly, which bypasses the CanExecute/IsRunning check
/// that would otherwise prevent re-entrant execution - without this, two captures racing on the
/// one shared primary window's Hide/Show could let one call's Show() bake the reappeared window
/// into the other's still-in-progress screenshot.
/// </summary>
public sealed class CaptureGate
{
    private int _isBusy;

    public bool TryBegin() => Interlocked.CompareExchange(ref _isBusy, 1, 0) == 0;

    public void End() => Interlocked.Exchange(ref _isBusy, 0);
}
