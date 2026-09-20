using System.Threading;

namespace FloppyGames.Core.Media;

/// <summary>Implementação real de <see cref="IRemovableDriveInspector"/> sobre o sistema de ficheiros.</summary>
public sealed class FileSystemDriveInspector : IRemovableDriveInspector
{
    public const string GameIniFileName = "GAME.INI";

    private const int MaxReadAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(300);

    public bool IsRemovableDrive(string driveRoot)
    {
        try
        {
            return new DriveInfo(driveRoot).DriveType == DriveType.Removable;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool TryReadGameIni(string driveRoot, out string? content)
    {
        var path = Path.Combine(driveRoot, GameIniFileName);

        // Disquetes físicas têm um motor mecânico que precisa de um instante para estabilizar
        // depois de o Windows reportar a unidade como pronta — sem retry, a primeira leitura
        // falha frequentemente com IOException em hardware real.
        for (var attempt = 1; attempt <= MaxReadAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    content = null;
                    return false;
                }

                content = File.ReadAllText(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == MaxReadAttempts)
                {
                    content = null;
                    return false;
                }

                Thread.Sleep(RetryDelay);
            }
        }

        content = null;
        return false;
    }
}
