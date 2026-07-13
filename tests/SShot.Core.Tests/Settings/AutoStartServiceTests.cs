using SShot.Core.Settings;

namespace SShot.Core.Tests.Settings;

public class AutoStartServiceTests
{
    private sealed class FakeRunKeyRegistry : IRunKeyRegistry
    {
        private readonly Dictionary<string, string> _values = [];

        public bool TryGetValue(string valueName, out object? value)
        {
            bool found = _values.TryGetValue(valueName, out var stringValue);
            value = stringValue;
            return found;
        }

        public void SetValue(string valueName, string value) => _values[valueName] = value;

        public void DeleteValue(string valueName) => _values.Remove(valueName);
    }

    [Fact]
    public void IsEnabled_InitiallyFalse_ForUnusedValueName()
    {
        var service = new AutoStartService(new FakeRunKeyRegistry(), "SShot");

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_ThenIsEnabled_True_ThenFalse_ThenIsEnabled_False()
    {
        var service = new AutoStartService(new FakeRunKeyRegistry(), "SShot");

        service.SetEnabled(true, @"C:\fake\path\SShot.exe");
        Assert.True(service.IsEnabled());

        service.SetEnabled(false, @"C:\fake\path\SShot.exe");
        Assert.False(service.IsEnabled());
    }
}
