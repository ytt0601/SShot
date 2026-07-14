namespace SShot.App.Services;

/// <summary>
/// Single shared gate so hotkeys, window buttons, and the tray menu can't trigger two overlapping
/// captures. RunHiddenAsync/RunGatedCaptureAsync (MainWindow, FloatingToolbarWindow,
/// GlobalHotkeyManager) call IAsyncRelayCommand.ExecuteAsync directly, which bypasses the
/// CanExecute/IsRunning check that would otherwise prevent re-entrant execution - without this,
/// two captures racing on the one shared primary window's Hide/Show could let one call's Show()
/// bake the reappeared window into the other's still-in-progress screenshot.
///
/// TryBeginScope() returns an IDisposable rather than exposing raw TryBegin()/End() so a caller
/// can wrap its entire capture body (including the Hide()/DwmSync.WaitForNextFrame() calls before
/// any try block) in a single `using` - the compiler-generated try/finally then guarantees the
/// gate is released even if one of those calls throws, instead of only being released when
/// End() happens to be reachable.
/// </summary>
public sealed class CaptureGate
{
    private int _isBusy;

    public IDisposable? TryBeginScope() =>
        Interlocked.CompareExchange(ref _isBusy, 1, 0) == 0 ? new Scope(this) : null;

    private sealed class Scope(CaptureGate gate) : IDisposable
    {
        public void Dispose() => Interlocked.Exchange(ref gate._isBusy, 0);
    }
}
