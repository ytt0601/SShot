using System.Windows.Input;

namespace SShot.Core.Settings;

/// <summary>
/// Parses/formats hotkey bindings as human-editable strings like "Ctrl+Shift+R" (so the JSON
/// settings file stays hand-editable by design). Pure string/enum
/// logic - no OS hotkey registration here, that's GlobalHotkeyManager (SShot.App).
/// </summary>
public static class HotkeyString
{
    public static bool TryParse(string? hotkeyString, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var parsedModifier = parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "shift" => ModifierKeys.Shift,
                "alt" => ModifierKeys.Alt,
                "win" or "windows" => ModifierKeys.Windows,
                _ => (ModifierKeys?)null,
            };

            if (parsedModifier is null)
            {
                modifiers = ModifierKeys.None;
                key = Key.None;
                return false;
            }

            modifiers |= parsedModifier.Value;
        }

        if (!Enum.TryParse(parts[^1], ignoreCase: true, out key) || key == Key.None)
        {
            modifiers = ModifierKeys.None;
            return false;
        }

        return true;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
