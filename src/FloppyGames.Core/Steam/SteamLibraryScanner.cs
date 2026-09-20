namespace FloppyGames.Core.Steam;

/// <summary>
/// Lê <c>libraryfolders.vdf</c> para encontrar todas as bibliotecas Steam (podem estar espalhadas
/// por vários discos) e depois os <c>appmanifest_*.acf</c> de cada uma para listar os jogos instalados.
/// </summary>
public sealed class SteamLibraryScanner
{
    private readonly ISteamFileSystem _fileSystem;
    private readonly ISteamPathProvider _pathProvider;

    public SteamLibraryScanner(ISteamFileSystem fileSystem, ISteamPathProvider pathProvider)
    {
        _fileSystem = fileSystem;
        _pathProvider = pathProvider;
    }

    public IReadOnlyList<InstalledSteamGame> ScanInstalledGames()
    {
        var steamPath = _pathProvider.GetSteamInstallPath();
        if (steamPath is null)
        {
            return [];
        }

        var games = new List<InstalledSteamGame>();

        foreach (var libraryFolder in GetLibraryFolders(steamPath))
        {
            var steamAppsDir = Path.Combine(libraryFolder, "steamapps");
            if (!_fileSystem.DirectoryExists(steamAppsDir))
            {
                continue;
            }

            foreach (var manifestPath in _fileSystem.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                var game = TryReadManifest(manifestPath, steamAppsDir);
                if (game is not null)
                {
                    games.Add(game);
                }
            }
        }

        return games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<string> GetLibraryFolders(string steamPath)
    {
        var folders = new List<string> { steamPath };
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

        if (!_fileSystem.FileExists(vdfPath))
        {
            return folders;
        }

        try
        {
            var root = VdfParser.Parse(_fileSystem.ReadAllText(vdfPath));
            foreach (var entry in root.Children.Values)
            {
                var path = entry.GetString("path");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    folders.Add(path);
                }
            }
        }
        catch (FormatException)
        {
            // libraryfolders.vdf corrompido ou em formato inesperado — segue só com a biblioteca principal.
        }

        return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private InstalledSteamGame? TryReadManifest(string manifestPath, string steamAppsDir)
    {
        try
        {
            var root = VdfParser.Parse(_fileSystem.ReadAllText(manifestPath));
            var appIdRaw = root.GetString("appid");
            var name = root.GetString("name");
            var installDir = root.GetString("installdir");

            if (appIdRaw is null || name is null || installDir is null || !int.TryParse(appIdRaw, out var appId))
            {
                return null;
            }

            var installPath = Path.Combine(steamAppsDir, "common", installDir);
            return new InstalledSteamGame(appId, name, installDir, installPath);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
