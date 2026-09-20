using FloppyGames.Core.Configuration;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Media;

/// <summary>
/// Apura os dados extra do ecrã de arranque (tamanho e CRC32 do suporte, se o jogo já está
/// instalado na Steam e o seu tamanho em disco). Pensada para correr fora da thread de UI —
/// ler de uma disquete física é lento, e procurar na biblioteca Steam envolve I/O.
/// </summary>
public sealed class GameLaunchSummaryBuilder
{
    private readonly SteamLibraryScanner _steamLibraryScanner;

    public GameLaunchSummaryBuilder(SteamLibraryScanner steamLibraryScanner)
    {
        _steamLibraryScanner = steamLibraryScanner;
    }

    public GameLaunchSummary Build(string driveRoot, GameConfig config)
    {
        var mediaKind = MediaKindClassifier.Classify(driveRoot);
        var (sizeBytes, crc32) = ComputeMediaFingerprint(driveRoot, config);

        InstalledSteamGame? installed = null;
        try
        {
            installed = _steamLibraryScanner.ScanInstalledGames().FirstOrDefault(g => g.AppId == config.AppId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Biblioteca Steam momentaneamente inacessível — segue sem o estado de instalação.
        }

        return new GameLaunchSummary(mediaKind, sizeBytes, crc32, installed is not null, installed?.SizeOnDiskBytes);
    }

    private static (long SizeBytes, uint Crc32) ComputeMediaFingerprint(string driveRoot, GameConfig config)
    {
        var files = new List<string> { Path.Combine(driveRoot, FileSystemDriveInspector.GameIniFileName) };
        if (!string.IsNullOrWhiteSpace(config.Cover))
        {
            files.Add(Path.Combine(driveRoot, config.Cover));
        }

        long totalBytes = 0;
        var crc = Crc32.InitialState;

        foreach (var file in files)
        {
            try
            {
                var bytes = File.ReadAllBytes(file);
                totalBytes += bytes.Length;
                crc = Crc32.Append(crc, bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // O suporte pode ter sido ejetado a meio da leitura — ignora este ficheiro e segue com o resto.
            }
        }

        return (totalBytes, Crc32.Finalize(crc));
    }
}
