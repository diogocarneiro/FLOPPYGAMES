using System.Text.Json;

namespace FloppyGames.Core.Settings;

/// <summary>
/// Persiste <see cref="AgentSettings"/> num pequeno ficheiro JSON em
/// <c>%LOCALAPPDATA%\FloppyGames\settings.json</c> — o único estado do Agent que não vive no
/// Registo (arranque automático) nem no próprio suporte (GAME.INI).
/// </summary>
public sealed class AgentSettingsStore
{
    private static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloppyGames", "settings.json");

    private readonly string _settingsPath;

    public AgentSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? DefaultSettingsPath;
    }

    public AgentSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AgentSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AgentSettings>(json) ?? new AgentSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AgentSettings();
        }
    }

    public void Save(AgentSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}
