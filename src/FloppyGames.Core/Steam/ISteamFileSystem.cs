namespace FloppyGames.Core.Steam;

/// <summary>
/// Abstrai o acesso ao sistema de ficheiros necessário para explorar a biblioteca Steam,
/// para que <see cref="SteamLibraryScanner"/> e <see cref="GameExecutableFinder"/> sejam
/// testáveis sem depender de uma instalação Steam real.
/// </summary>
public interface ISteamFileSystem
{
    public bool FileExists(string path);

    public bool DirectoryExists(string path);

    public string ReadAllText(string path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive = false);
}
