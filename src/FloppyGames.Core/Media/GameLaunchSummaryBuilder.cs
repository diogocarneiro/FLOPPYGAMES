using FloppyGames.Core.Configuration;
using FloppyGames.Core.Platforms;
using FloppyGames.Core.Settings;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Media;

/// <summary>
/// Apura os dados extra do ecrã de arranque: tamanho do suporte, estado de instalação e, quando
/// disponível, build/atualização/tempo de jogo/conquistas (exclusivos da Steam — sem equivalente
/// local fiável na Epic/GOG). A parte local corre fora da thread de UI (I/O que pode ser lento,
/// sobretudo numa disquete física); a parte de conquistas depende de rede, por isso é sempre
/// assíncrona.
/// </summary>
public sealed class GameLaunchSummaryBuilder
{
    private readonly SteamLibraryScanner _steamLibraryScanner;
    private readonly SteamPlaytimeReader _playtimeReader;
    private readonly ISteamAchievementsProvider _achievementsProvider;
    private readonly AgentSettingsStore _settingsStore;
    private readonly EpicGameLibraryScanner _epicLibraryScanner;
    private readonly GogGameLibraryScanner _gogLibraryScanner;

    public GameLaunchSummaryBuilder(
        SteamLibraryScanner steamLibraryScanner,
        SteamPlaytimeReader playtimeReader,
        ISteamAchievementsProvider achievementsProvider,
        AgentSettingsStore settingsStore,
        EpicGameLibraryScanner epicLibraryScanner,
        GogGameLibraryScanner gogLibraryScanner)
    {
        _steamLibraryScanner = steamLibraryScanner;
        _playtimeReader = playtimeReader;
        _achievementsProvider = achievementsProvider;
        _settingsStore = settingsStore;
        _epicLibraryScanner = epicLibraryScanner;
        _gogLibraryScanner = gogLibraryScanner;
    }

    public async Task<GameLaunchSummary> BuildAsync(string driveRoot, GameConfig config, CancellationToken cancellationToken)
    {
        var local = await Task.Run(() => BuildLocalData(driveRoot, config), cancellationToken);

        AchievementSummary? achievements = null;
        if (config.Platform == GamePlatform.Steam
            && local.SteamOwnerSteamId64 is { } ownerSteamId64
            && config.AppId is { } steamAppId
            && !string.IsNullOrWhiteSpace(local.SteamWebApiKey))
        {
            achievements = await _achievementsProvider.TryGetSummaryAsync(
                local.SteamWebApiKey, ownerSteamId64, steamAppId, cancellationToken);
        }

        return new GameLaunchSummary(
            config.Platform,
            local.MediaKind,
            local.MediaSizeBytes,
            local.IsInstalled,
            local.InstalledSizeBytes,
            local.BuildId,
            local.LastUpdatedUtc,
            local.LastPlayedUtc,
            local.PlaytimeMinutes,
            achievements);
    }

    private LocalData BuildLocalData(string driveRoot, GameConfig config)
    {
        var mediaKind = MediaKindClassifier.Classify(driveRoot);
        var mediaSizeBytes = ComputeMediaSizeBytes(driveRoot, config);

        return config.Platform switch
        {
            GamePlatform.Steam => BuildSteamData(config, mediaKind, mediaSizeBytes),
            GamePlatform.Epic => BuildEpicData(config, mediaKind, mediaSizeBytes),
            GamePlatform.Gog => BuildGogData(config, mediaKind, mediaSizeBytes),
            _ => new LocalData(mediaKind, mediaSizeBytes, false, null, null, null, null, null, null, null),
        };
    }

    private LocalData BuildSteamData(GameConfig config, MediaKind mediaKind, long mediaSizeBytes)
    {
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
        if (installed?.LastOwnerSteamId64 is { } ownerSteamId64 && config.AppId is { } appId)
        {
            try
            {
                playtimeMinutes = _playtimeReader.TryGetPlaytimeMinutes(appId, ownerSteamId64);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // localconfig.vdf momentaneamente inacessível — segue sem o tempo de jogo.
            }
        }

        var apiKey = _settingsStore.Load().SteamWebApiKey;

        return new LocalData(
            mediaKind, mediaSizeBytes, installed is not null, installed?.SizeOnDiskBytes,
            installed?.BuildId, installed?.LastUpdatedUtc, installed?.LastPlayedUtc, playtimeMinutes,
            installed?.LastOwnerSteamId64, apiKey);
    }

    private LocalData BuildEpicData(GameConfig config, MediaKind mediaKind, long mediaSizeBytes)
    {
        DiscoveredGame? installed = null;
        try
        {
            installed = _epicLibraryScanner.ScanInstalledGames().FirstOrDefault(g => g.EpicItemId == config.EpicItemId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Manifestos da Epic momentaneamente inacessíveis — segue sem o estado de instalação.
        }

        return new LocalData(
            mediaKind, mediaSizeBytes, installed is not null, installed?.InstalledSizeBytes,
            null, null, null, null, null, null);
    }

    private LocalData BuildGogData(GameConfig config, MediaKind mediaKind, long mediaSizeBytes)
    {
        DiscoveredGame? installed = null;
        try
        {
            installed = _gogLibraryScanner.ScanInstalledGames().FirstOrDefault(g => g.GogGameId == config.GogGameId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Registo GOG momentaneamente inacessível — segue sem o estado de instalação.
        }

        return new LocalData(
            mediaKind, mediaSizeBytes, installed is not null, installed?.InstalledSizeBytes,
            null, null, null, null, null, null);
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
        MediaKind MediaKind,
        long MediaSizeBytes,
        bool IsInstalled,
        long? InstalledSizeBytes,
        string? BuildId,
        DateTime? LastUpdatedUtc,
        DateTime? LastPlayedUtc,
        long? PlaytimeMinutes,
        ulong? SteamOwnerSteamId64,
        string? SteamWebApiKey);
}
