using System.Text.Json;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Platforms;

/// <summary>
/// Lê os manifestos da Epic Games Launcher
/// (<c>%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>, JSON simples, um ficheiro
/// por jogo instalado — formato confirmado contra um manifesto real desta máquina). Ao contrário
/// da Steam, o caminho é fixo: não precisa de registo para o encontrar.
/// </summary>
public sealed class EpicGameLibraryScanner
{
    private static readonly string DefaultManifestsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    private readonly ISteamFileSystem _fileSystem;
    private readonly string _manifestsDirectory;

    public EpicGameLibraryScanner(ISteamFileSystem fileSystem, string? manifestsDirectory = null)
    {
        _fileSystem = fileSystem;
        _manifestsDirectory = manifestsDirectory ?? DefaultManifestsDirectory;
    }

    public IReadOnlyList<DiscoveredGame> ScanInstalledGames()
    {
        if (!_fileSystem.DirectoryExists(_manifestsDirectory))
        {
            return [];
        }

        var games = new List<DiscoveredGame>();

        foreach (var manifestPath in _fileSystem.EnumerateFiles(_manifestsDirectory, "*.item"))
        {
            var game = TryReadManifest(manifestPath);
            if (game is not null)
            {
                games.Add(game);
            }
        }

        return games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private DiscoveredGame? TryReadManifest(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(_fileSystem.ReadAllText(manifestPath));
            var root = document.RootElement;

            var name = GetString(root, "DisplayName");
            var installLocation = GetString(root, "InstallLocation");
            var epicNamespace = GetString(root, "CatalogNamespace");
            var epicItemId = GetString(root, "CatalogItemId");
            var epicAppName = GetString(root, "AppName");

            if (name is null || installLocation is null || epicNamespace is null || epicItemId is null || epicAppName is null)
            {
                return null;
            }

            var installSizeBytes = root.TryGetProperty("InstallSize", out var sizeProperty)
                && sizeProperty.ValueKind == JsonValueKind.Number
                ? sizeProperty.GetInt64()
                : (long?)null;

            return new DiscoveredGame(
                name, GamePlatform.Epic, installLocation, installSizeBytes,
                EpicNamespace: epicNamespace, EpicItemId: epicItemId, EpicAppName: epicAppName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
