namespace FloppyGames.Core.Media;

/// <summary>Implementação real de <see cref="IDriveReadinessProbe"/> sobre <see cref="DriveInfo"/>.</summary>
public sealed class DriveInfoReadinessProbe : IDriveReadinessProbe
{
    public bool IsReady(string driveRoot)
    {
        try
        {
            return new DriveInfo(driveRoot).IsReady;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
