using FloppyGames.Core.Configuration;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Media;

/// <summary>
/// Apura os dados extra do ecrã de arranque: tamanho do suporte, estado de instalação na Steam
/// (build, data de atualização, tempo de jogo, última sessão). Pensada para correr fora da thread
/// de UI — ler de uma disquete física é lento, e procurar na biblioteca Steam envolve I/O.
/// </summary>
public sealed class GameLaunchSummaryBuilder
{
    private readonly SteamLibraryScanner _steamLibraryScanner;
    private readonly SteamPlaytimeReader _playtimeReader;

    public GameLaunchSummaryBuilder(SteamLibraryScanner steamLibraryScanner, SteamPlaytimeReader playtimeReader)
    {
        _steamLibraryScanner = steamLibraryScanner;
        _playtimeReader = playtimeReader;
    }

    public GameLaunchSummary Build(string driveRoot, GameConfig config)
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

        return new GameLaunchSummary(
            mediaKind,
            mediaSizeBytes,
            installed is not null,
            installed?.SizeOnDiskBytes,
            installed?.BuildId,
            installed?.LastUpdatedUtc,
            installed?.LastPlayedUtc,
            playtimeMinutes);
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
}
