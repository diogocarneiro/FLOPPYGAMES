using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

/// <summary>Dispara o lançamento de um jogo, delegando na loja/launcher a validação/atualização.</summary>
public interface IGameLauncher
{
    public void Launch(GameConfig config);
}
