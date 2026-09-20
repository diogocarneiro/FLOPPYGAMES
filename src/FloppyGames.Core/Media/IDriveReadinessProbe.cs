namespace FloppyGames.Core.Media;

/// <summary>
/// Abstrai a verificação de "há um disco pronto nesta unidade?", para que
/// <see cref="PollingFloppyDriveWatcher"/> seja testável sem hardware real.
/// </summary>
public interface IDriveReadinessProbe
{
    public bool IsReady(string driveRoot);
}
