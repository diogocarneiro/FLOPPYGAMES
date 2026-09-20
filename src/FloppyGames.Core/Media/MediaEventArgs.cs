using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Media;

public sealed class MediaInsertedEventArgs : EventArgs
{
    public MediaInsertedEventArgs(string driveRoot, GameConfig config)
    {
        DriveRoot = driveRoot;
        Config = config;
    }

    public string DriveRoot { get; }

    public GameConfig Config { get; }
}

public sealed class MediaRemovedEventArgs : EventArgs
{
    public MediaRemovedEventArgs(string driveRoot, GameConfig config)
    {
        DriveRoot = driveRoot;
        Config = config;
    }

    public string DriveRoot { get; }

    /// <summary>Configuração do jogo que estava associado a esta unidade, para o Agent saber que processo terminar.</summary>
    public GameConfig Config { get; }
}

public sealed class InvalidMediaEventArgs : EventArgs
{
    public InvalidMediaEventArgs(string driveRoot, IReadOnlyList<string> errors)
    {
        DriveRoot = driveRoot;
        Errors = errors;
    }

    public string DriveRoot { get; }

    public IReadOnlyList<string> Errors { get; }
}
