namespace FloppyGames.Core.Startup;

/// <summary>
/// Liga/desliga o arranque automático do FloppyGames Agent com o Windows.
/// Reversível a qualquer momento (instalador ou menu da bandeja), sem reinstalar.
/// </summary>
public sealed class AutostartManager
{
    private const string ValueName = "FloppyGamesAgent";

    private readonly IAutostartRegistry _registry;
    private readonly string _executablePath;

    public AutostartManager(IAutostartRegistry registry, string executablePath)
    {
        _registry = registry;
        _executablePath = executablePath;
    }

    public bool IsEnabled => _registry.GetValue(ValueName) is not null;

    public void Enable() => _registry.SetValue(ValueName, $"\"{_executablePath}\"");

    public void Disable() => _registry.DeleteValue(ValueName);
}
