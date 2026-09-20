using System.Diagnostics;

namespace FloppyGames.Core.Launch;

/// <summary>Lança jogos invocando o protocolo <c>steam://run/&lt;appId&gt;</c> via shell.</summary>
public sealed class SteamProtocolLauncher : ISteamLauncher
{
    public void Launch(int appId)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://run/{appId}",
            UseShellExecute = true,
        });
    }
}
