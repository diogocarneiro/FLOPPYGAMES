using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

/// <summary>
/// Despacha para o lançador certo consoante <see cref="GameConfig.Platform"/> — mesmo espírito do
/// <see cref="Media.CompositeRemovableMediaWatcher"/>, que já funde várias fontes atrás de uma
/// única interface.
/// </summary>
public sealed class CompositeGameLauncher : IGameLauncher
{
    private readonly IReadOnlyDictionary<GamePlatform, IGameLauncher> _launchers;

    public CompositeGameLauncher(IReadOnlyDictionary<GamePlatform, IGameLauncher> launchers)
    {
        _launchers = launchers;
    }

    public void Launch(GameConfig config)
    {
        if (!_launchers.TryGetValue(config.Platform, out var launcher))
        {
            throw new InvalidOperationException($"Plataforma '{config.Platform}' sem lançador configurado.");
        }

        launcher.Launch(config);
    }
}
