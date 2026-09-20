namespace FloppyGames.Core.Steam;

/// <summary>Um jogo Steam instalado localmente, lido a partir de um <c>appmanifest_*.acf</c>.</summary>
public sealed record InstalledSteamGame(
    int AppId,
    string Name,
    string InstallDirectory,
    string InstallPath,
    long? SizeOnDiskBytes = null,
    string? BuildId = null,
    DateTime? LastUpdatedUtc = null,
    DateTime? LastPlayedUtc = null,
    ulong? LastOwnerSteamId64 = null);
