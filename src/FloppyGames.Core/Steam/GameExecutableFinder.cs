namespace FloppyGames.Core.Steam;

/// <summary>Percorre a pasta de instalação de um jogo e sugere o executável principal.</summary>
public sealed class GameExecutableFinder
{
    private readonly ISteamFileSystem _fileSystem;

    public GameExecutableFinder(ISteamFileSystem fileSystem) => _fileSystem = fileSystem;

    public string? FindSuggestedExecutable(string installPath)
    {
        if (!_fileSystem.DirectoryExists(installPath))
        {
            return null;
        }

        var exeFileNames = _fileSystem.EnumerateFiles(installPath, "*.exe", recursive: true)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var installDirectoryName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return ExecutableSuggester.Suggest(exeFileNames, installDirectoryName);
    }
}
