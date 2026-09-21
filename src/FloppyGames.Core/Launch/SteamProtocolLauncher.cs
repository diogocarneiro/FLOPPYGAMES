using System.Diagnostics;
using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

/// <summary>Lança jogos invocando o protocolo <c>steam://run/&lt;appId&gt;</c> via shell.</summary>
public sealed class SteamProtocolLauncher : IGameLauncher
{
    public void Launch(GameConfig config)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://run/{config.AppId}",
            UseShellExecute = true,
        });
    }
}
