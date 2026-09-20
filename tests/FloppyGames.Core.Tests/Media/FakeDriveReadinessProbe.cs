using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

internal sealed class FakeDriveReadinessProbe : IDriveReadinessProbe
{
    private readonly Dictionary<string, bool> _ready = new(StringComparer.OrdinalIgnoreCase);

    public void SetReady(string driveRoot, bool ready) => _ready[driveRoot] = ready;

    public bool IsReady(string driveRoot) => _ready.GetValueOrDefault(driveRoot);
}
