using System.Diagnostics;
using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

/// <summary>
/// Lança jogos invocando o URI documentado pela Epic Games Launcher
/// (<c>com.epicgames.launcher://apps/{namespace}%3A{item}%3A{appName}?action=launch&amp;silent=true</c>),
/// o mesmo padrão do <c>steam://run/</c> — os três valores vêm do manifesto local do jogo.
/// </summary>
public sealed class EpicProtocolLauncher : IGameLauncher
{
    public void Launch(GameConfig config)
    {
        var uri = "com.epicgames.launcher://apps/" +
                   $"{config.EpicNamespace}%3A{config.EpicItemId}%3A{config.EpicAppName}?action=launch&silent=true";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true,
        });
    }
}
