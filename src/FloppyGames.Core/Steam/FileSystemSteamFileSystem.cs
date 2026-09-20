namespace FloppyGames.Core.Steam;

/// <summary>Implementação real de <see cref="ISteamFileSystem"/> sobre <see cref="System.IO"/>.</summary>
public sealed class FileSystemSteamFileSystem : ISteamFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive = false)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
        };

        try
        {
            return Directory.EnumerateFiles(directory, searchPattern, options).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
