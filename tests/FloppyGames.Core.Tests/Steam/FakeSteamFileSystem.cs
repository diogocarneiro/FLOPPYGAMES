using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

internal sealed class FakeSteamFileSystem : ISteamFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public FakeSteamFileSystem WithFile(string path, string content)
    {
        _files[path] = content;
        _directories.Add(Path.GetDirectoryName(path) ?? string.Empty);
        return this;
    }

    public FakeSteamFileSystem WithDirectory(string path)
    {
        _directories.Add(path);
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public string ReadAllText(string path) => _files[path];

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive = false)
    {
        var starIndex = searchPattern.IndexOf('*');
        var prefix = starIndex < 0 ? searchPattern : searchPattern[..starIndex];
        var suffix = starIndex < 0 ? string.Empty : searchPattern[(starIndex + 1)..];

        foreach (var path in _files.Keys)
        {
            var fileDir = Path.GetDirectoryName(path) ?? string.Empty;
            var withinScope = recursive
                ? fileDir.StartsWith(directory, StringComparison.OrdinalIgnoreCase)
                : string.Equals(fileDir, directory, StringComparison.OrdinalIgnoreCase);

            if (!withinScope)
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return path;
        }
    }
}
