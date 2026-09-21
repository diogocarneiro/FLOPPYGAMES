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
        sb.AppendLine($"PLATFORM={config.Platform.ToString().ToUpperInvariant()}");

        switch (config.Platform)
        {
            case GamePlatform.Steam:
                sb.AppendLine($"APPID={config.AppId}");
                break;
            case GamePlatform.Epic:
                sb.AppendLine($"EPIC_NAMESPACE={config.EpicNamespace}");
                sb.AppendLine($"EPIC_ITEM={config.EpicItemId}");
                sb.AppendLine($"EPIC_APP={config.EpicAppName}");
                break;
            case GamePlatform.Gog:
                sb.AppendLine($"GOG_ID={config.GogGameId}");
                break;
        }

        sb.AppendLine($"PROCESS={config.Process}");
        if (config.Cover is not null)
        {
            sb.AppendLine($"COVER={config.Cover}");
        }

        if (config.Description is not null)
        {
            sb.AppendLine($"DESCRIPTION={config.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("[Options]");
        sb.AppendLine($"WatchTimeoutSeconds={config.WatchTimeoutSeconds}");
        sb.AppendLine($"LaunchDelaySeconds={config.LaunchDelaySeconds}");
        sb.AppendLine($"GracefulShutdown={(config.GracefulShutdown ? "true" : "false")}");

        return sb.ToString();
    }
}
