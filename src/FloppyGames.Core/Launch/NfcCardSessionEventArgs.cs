using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

public sealed class NfcCardLaunchStartingEventArgs : EventArgs
{
    public NfcCardLaunchStartingEventArgs(string uid, GameConfig config)
    {
        Uid = uid;
        Config = config;
    }

    public string Uid { get; }

    public GameConfig Config { get; }
}

public sealed class NfcCardLaunchedEventArgs : EventArgs
{
    public NfcCardLaunchedEventArgs(string uid, GameConfig config)
    {
        Uid = uid;
        Config = config;
    }

    public string Uid { get; }

    public GameConfig Config { get; }
}

public sealed class NfcCardLaunchFailedEventArgs : EventArgs
{
    public NfcCardLaunchFailedEventArgs(string uid, GameConfig config, GameLaunchFailureReason reason)
    {
        Uid = uid;
        Config = config;
        Reason = reason;
    }

    public string Uid { get; }

    public GameConfig Config { get; }

    public GameLaunchFailureReason Reason { get; }
}

public sealed class NfcCardStoppedEventArgs : EventArgs
{
    public NfcCardStoppedEventArgs(string uid, GameConfig config)
    {
        Uid = uid;
        Config = config;
    }

    public string Uid { get; }

    public GameConfig Config { get; }
}
