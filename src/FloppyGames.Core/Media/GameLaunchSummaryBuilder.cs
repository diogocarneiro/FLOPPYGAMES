using FloppyGames.Core.Configuration;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Media;

/// <summary>
/// Apura os dados extra do ecrã de arranque: tamanho do suporte, estado de instalação na Steam
/// (build, data de atualização, tempo de jogo, última sessão, conquistas). A parte local corre
/// fora da thread de UI (I/O que pode ser lento, sobretudo numa disquete física); a parte de
/// conquistas depende de rede, por isso é sempre assíncrona.
/// </summary>
public sealed class GameLaunchSummaryBuilder
{
    private readonly SteamLibraryScanner _steamLibraryScanner;
    private readonly SteamPlaytimeReader _playtimeReader;
    private readonly ISteamAchievementsProvider _achievementsProvider;
    private readonly AgentSettingsStore _settingsStore;

    public GameLaunchSummaryBuilder(
        SteamLibraryScanner steamLibraryScanner,
        SteamPlaytimeReader playtimeReader,
        ISteamAchievementsProvider achievementsProvider,
        AgentSettingsStore settingsStore)
    {
        _steamLibraryScanner = steamLibraryScanner;
        _playtimeReader = playtimeReader;
        _achievementsProvider = achievementsProvider;
        _settingsStore = settingsStore;
    }

    public async Task<GameLaunchSummary> BuildAsync(string driveRoot, GameConfig config, CancellationToken cancellationToken)
    {
        var local = await Task.Run(() => BuildLocalData(driveRoot, config), cancellationToken);

        AchievementSummary? achievements = null;
        if (local.Installed?.LastOwnerSteamId64 is { } ownerSteamId64 && !string.IsNullOrWhiteSpace(local.SteamWebApiKey))
        {
            achievements = await _achievementsProvider.TryGetSummaryAsync(
                local.SteamWebApiKey, ownerSteamId64, config.AppId, cancellationToken);
        }

        return new GameLaunchSummary(
            local.MediaKind,
            local.MediaSizeBytes,
            local.Installed is not null,
            local.Installed?.SizeOnDiskBytes,
            local.Installed?.BuildId,
            local.Installed?.LastUpdatedUtc,
            local.Installed?.LastPlayedUtc,
            local.PlaytimeMinutes,
            achievements);
    }

    private LocalData BuildLocalData(string driveRoot, GameConfig config)
    {
        var mediaKind = MediaKindClassifier.Classify(driveRoot);
        var mediaSizeBytes = ComputeMediaSizeBytes(driveRoot, config);

        InstalledSteamGame? installed = null;
        try
        {
            installed = _steamLibraryScanner.ScanInstalledGames().FirstOrDefault(g => g.AppId == config.AppId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Biblioteca Steam momentaneamente inacessível — segue sem o estado de instalação.
        }

        long? playtimeMinutes = null;
        if (installed?.LastOwnerSteamId64 is { } ownerSteamId64)
        {
            try
            {
                playtimeMinutes = _playtimeReader.TryGetPlaytimeMinutes(config.AppId, ownerSteamId64);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // localconfig.vdf momentaneamente inacessível — segue sem o tempo de jogo.
            }
        }

        var apiKey = _settingsStore.Load().SteamWebApiKey;

        return new LocalData(mediaKind, mediaSizeBytes, installed, playtimeMinutes, apiKey);
    }

    private static long ComputeMediaSizeBytes(string driveRoot, GameConfig config)
    {
        var files = new List<string> { Path.Combine(driveRoot, FileSystemDriveInspector.GameIniFileName) };
        if (!string.IsNullOrWhiteSpace(config.Cover))
        {
            files.Add(Path.Combine(driveRoot, config.Cover));
        }

        long totalBytes = 0;

        foreach (var file in files)
        {
            try
            {
                totalBytes += new FileInfo(file).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // O suporte pode ter sido ejetado a meio da leitura — ignora este ficheiro e segue com o resto.
            }
        }

        return totalBytes;
    }

    private sealed record LocalData(
        MediaKind MediaKind, long MediaSizeBytes, InstalledSteamGame? Installed, long? PlaytimeMinutes, string? SteamWebApiKey);
}
