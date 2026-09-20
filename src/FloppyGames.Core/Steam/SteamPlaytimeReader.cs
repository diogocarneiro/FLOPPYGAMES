using System.Globalization;

namespace FloppyGames.Core.Steam;

/// <summary>
/// Lê o tempo de jogo total (minutos) de <c>userdata/&lt;SteamID3&gt;/config/localconfig.vdf</c> —
/// o único sítio onde a Steam guarda isto localmente; não consta do <c>appmanifest_*.acf</c>.
/// O SteamID3 (nome da pasta em <c>userdata</c>) deriva-se do SteamID64 do último dono do jogo
/// (<c>AppState.LastOwner</c>, já lido por <see cref="SteamLibraryScanner"/>) subtraindo a base fixa
/// das contas Steam individuais.
/// </summary>
public sealed class SteamPlaytimeReader
{
    private const ulong IndividualAccountBase = 76561197960265728UL;

    private readonly ISteamFileSystem _fileSystem;
    private readonly ISteamPathProvider _pathProvider;

    public SteamPlaytimeReader(ISteamFileSystem fileSystem, ISteamPathProvider pathProvider)
    {
        _fileSystem = fileSystem;
        _pathProvider = pathProvider;
    }

    public long? TryGetPlaytimeMinutes(int appId, ulong ownerSteamId64)
    {
        var steamPath = _pathProvider.GetSteamInstallPath();
        if (steamPath is null || ownerSteamId64 < IndividualAccountBase)
        {
            return null;
        }

        var steamId3 = ownerSteamId64 - IndividualAccountBase;
        var localConfigPath = Path.Combine(
            steamPath, "userdata", steamId3.ToString(CultureInfo.InvariantCulture), "config", "localconfig.vdf");

        if (!_fileSystem.FileExists(localConfigPath))
        {
            return null;
        }

        try
        {
            var root = VdfParser.Parse(_fileSystem.ReadAllText(localConfigPath));
            var appNode = root["Software"]?["Valve"]?["Steam"]?["apps"]?[appId.ToString(CultureInfo.InvariantCulture)];
            return long.TryParse(appNode?.GetString("Playtime"), out var minutes) ? minutes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
