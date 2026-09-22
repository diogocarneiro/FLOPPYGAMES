using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Nfc;

public sealed class CardInsertedEventArgs : EventArgs
{
    public CardInsertedEventArgs(string uid, GameConfig config)
    {
        Uid = uid;
        Config = config;
    }

    public string Uid { get; }

    public GameConfig Config { get; }
}

public sealed class CardRemovedEventArgs : EventArgs
{
    public CardRemovedEventArgs(string uid, GameConfig config)
    {
        Uid = uid;
        Config = config;
    }

    public string Uid { get; }

    /// <summary>Configuração do jogo que estava associada a este cartão, para o Agent saber que processo terminar.</summary>
    public GameConfig Config { get; }
}

public sealed class InvalidCardEventArgs : EventArgs
{
    public InvalidCardEventArgs(string uid, IReadOnlyList<string> errors)
    {
        Uid = uid;
        Errors = errors;
    }

    public string Uid { get; }

    public IReadOnlyList<string> Errors { get; }
}
