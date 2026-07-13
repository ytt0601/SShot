using Microsoft.Win32;

namespace SShot.Core.Settings;

/// <summary>
/// Toggles the HKCU Run key entry. Per-user (not machine-wide) so no admin rights are needed
/// and the toggle works identically for the portable exe and the installed exe - both the
/// in-app Settings checkbox and the installer's "launch at startup" checkbox call this same
/// logic (see CLAUDE.md).
/// </summary>
public sealed class AutoStartService
{
    private readonly IRunKeyRegistry _registry;
    private readonly string _valueName;

    public AutoStartService(string valueName = "SShot")
        : this(new Win32RunKeyRegistry(), valueName)
    {
    }

    internal AutoStartService(IRunKeyRegistry registry, string valueName)
    {
        _registry = registry;
        _valueName = valueName;
    }

    public bool IsEnabled() => _registry.TryGetValue(_valueName, out _);

    public void SetEnabled(bool enabled, string executablePath)
    {
        if (enabled)
        {
            _registry.SetValue(_valueName, $"\"{executablePath}\"");
        }
        else
        {
            _registry.DeleteValue(_valueName);
        }
    }
}

internal sealed class Win32RunKeyRegistry : IRunKeyRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool TryGetValue(string valueName, out object? value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        value = key?.GetValue(valueName);
        return value is not null;
    }

    public void SetValue(string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(valueName, value);
    }

    public void DeleteValue(string valueName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
