using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Launch;

internal sealed record GameSession(GameConfig Config, IManagedProcess Process);
