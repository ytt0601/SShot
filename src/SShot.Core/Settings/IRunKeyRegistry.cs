namespace SShot.Core.Settings;

/// <summary>
/// Seam around the HKCU Run key entry so AutoStartService's registry access is swappable in
/// tests (see CLAUDE.md's rule that external dependencies in Core - P/Invoke, file I/O,
/// registry - must go through an interface). Without this, tests had to mutate the real user
/// registry (via a throwaway value name) just to exercise AutoStartService.
/// </summary>
public interface IRunKeyRegistry
{
    bool TryGetValue(string valueName, out object? value);

    void SetValue(string valueName, string value);

    void DeleteValue(string valueName);
}
