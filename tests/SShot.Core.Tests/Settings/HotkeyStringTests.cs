using System.Windows.Input;
using SShot.Core.Settings;

namespace SShot.Core.Tests.Settings;

public class HotkeyStringTests
{
    [Theory]
    [InlineData("Ctrl+Shift+R", ModifierKeys.Control | ModifierKeys.Shift, Key.R)]
    [InlineData("ctrl+alt+F", ModifierKeys.Control | ModifierKeys.Alt, Key.F)]
    [InlineData("Win+S", ModifierKeys.Windows, Key.S)]
    [InlineData("Control+Shift+Alt+W", ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, Key.W)]
    public void TryParse_ValidStrings_ParsesModifiersAndKey(string input, ModifierKeys expectedModifiers, Key expectedKey)
    {
        bool ok = HotkeyString.TryParse(input, out var modifiers, out var key);

        Assert.True(ok);
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("R")] // no modifier
    [InlineData("Ctrl+")] // missing key
    [InlineData("Ctrl+NotAKey")]
    [InlineData(null)]
    public void TryParse_InvalidStrings_ReturnsFalse(string? input)
    {
        bool ok = HotkeyString.TryParse(input, out var modifiers, out var key);

        Assert.False(ok);
        Assert.Equal(ModifierKeys.None, modifiers);
        Assert.Equal(Key.None, key);
    }

    [Fact]
    public void Format_ThenParse_RoundTrips()
    {
        var modifiers = ModifierKeys.Control | ModifierKeys.Shift;
        const Key key = Key.R;

        string formatted = HotkeyString.Format(modifiers, key);
        bool ok = HotkeyString.TryParse(formatted, out var parsedModifiers, out var parsedKey);

        Assert.True(ok);
        Assert.Equal(modifiers, parsedModifiers);
        Assert.Equal(key, parsedKey);
    }
}
