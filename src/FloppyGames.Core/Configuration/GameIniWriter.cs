using System.Text;

namespace FloppyGames.Core.Configuration;

/// <summary>Serializa uma <see cref="GameConfig"/> de volta para o formato GAME.INI.</summary>
public static class GameIniWriter
{
    public static string Write(GameConfig config)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Game]");
        sb.AppendLine($"TITLE={config.Title}");
        sb.AppendLine($"APPID={config.AppId}");
        sb.AppendLine($"PROCESS={config.Process}");
        if (config.Cover is not null)
        {
            sb.AppendLine($"COVER={config.Cover}");
        }

        sb.AppendLine();
        sb.AppendLine("[Options]");
        sb.AppendLine($"WatchTimeoutSeconds={config.WatchTimeoutSeconds}");
        sb.AppendLine($"LaunchDelaySeconds={config.LaunchDelaySeconds}");
        sb.AppendLine($"GracefulShutdown={(config.GracefulShutdown ? "true" : "false")}");

        return sb.ToString();
    }
}
