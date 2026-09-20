namespace FloppyGames.Core.Media;

/// <summary>
/// Dados adicionais sobre o lançamento em curso, apurados a partir do próprio suporte e da
/// biblioteca Steam local, para o ecrã de arranque mostrar — no estilo dos antigos ecrãs de
/// carregamento com verificação de disco.
/// </summary>
public sealed record GameLaunchSummary(
    MediaKind MediaKind,
    long MediaSizeBytes,
    uint MediaCrc32,
    bool IsInstalledOnSteam,
    long? InstalledSizeBytes);
