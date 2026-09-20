using FloppyGames.Core.Startup;

namespace FloppyGames.Core.Tests.Startup;

internal sealed class FakeAutostartRegistry : IAutostartRegistry
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string? GetValue(string valueName) => _values.GetValueOrDefault(valueName);

    public void SetValue(string valueName, string value) => _values[valueName] = value;

    public void DeleteValue(string valueName) => _values.Remove(valueName);
}
