namespace FloppyGames.Core.Launch;

/// <summary>Dispara o lançamento de um jogo Steam, delegando na Steam a validação/atualização.</summary>
public interface ISteamLauncher
{
    public void Launch(int appId);
}
