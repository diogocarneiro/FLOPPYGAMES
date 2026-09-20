namespace FloppyGames.Core.Steam;

/// <summary>Localiza a pasta de instalação do cliente Steam.</summary>
public interface ISteamPathProvider
{
    public string? GetSteamInstallPath();
}
