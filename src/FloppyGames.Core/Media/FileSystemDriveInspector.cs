namespace FloppyGames.Core.Media;

/// <summary>Implementação real de <see cref="IRemovableDriveInspector"/> sobre o sistema de ficheiros.</summary>
public sealed class FileSystemDriveInspector : IRemovableDriveInspector
{
    public const string GameIniFileName = "GAME.INI";

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
            content = null;
            return false;
        }
    }
}
