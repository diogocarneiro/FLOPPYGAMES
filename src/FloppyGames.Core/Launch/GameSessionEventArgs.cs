using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

public sealed class GameLaunchStartingEventArgs : EventArgs
{
    public GameLaunchStartingEventArgs(string driveRoot, GameConfig config)
    {
        DriveRoot = driveRoot;
        Config = config;
    }

    public string DriveRoot { get; }

    public GameConfig Config { get; }
}

public sealed class GameLaunchedEventArgs : EventArgs
{
    public GameLaunchedEventArgs(string driveRoot, GameConfig config)
    {
        DriveRoot = driveRoot;
        Config = config;
    }

    public string DriveRoot { get; }

    public GameConfig Config { get; }
}

public sealed class GameLaunchFailedEventArgs : EventArgs
{
    public GameLaunchFailedEventArgs(string driveRoot, GameConfig config, string reason)
    {
        DriveRoot = driveRoot;
        Config = config;
        Reason = reason;
    }

    public string DriveRoot { get; }

    public GameConfig Config { get; }

    public string Reason { get; }
}

public sealed class GameStoppedEventArgs : EventArgs
{
    public GameStoppedEventArgs(string driveRoot, GameConfig config)
    {
        DriveRoot = driveRoot;
        Config = config;
    }

    public string DriveRoot { get; }

    public GameConfig Config { get; }
}
