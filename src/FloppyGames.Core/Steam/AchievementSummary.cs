namespace FloppyGames.Core.Steam;

/// <summary>Número de conquistas desbloqueadas de um jogo, para um utilizador Steam específico.</summary>
public sealed record AchievementSummary(int Unlocked, int Total);
