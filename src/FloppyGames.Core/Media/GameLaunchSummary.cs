namespace FloppyGames.Core.Media;

/// <summary>
/// Dados adicionais sobre o lançamento em curso, apurados a partir do próprio suporte e da
/// biblioteca Steam local, para o ecrã de arranque mostrar. Os campos a partir de
/// <see cref="BuildId"/> só fazem sentido (e só vêm preenchidos) quando o jogo já está instalado.
/// </summary>
public sealed record GameLaunchSummary(
    MediaKind MediaKind,
    long MediaSizeBytes,
    bool IsInstalledOnSteam,
    long? InstalledSizeBytes,
    string? BuildId,
    DateTime? LastUpdatedUtc,
    DateTime? LastPlayedUtc,
    long? PlaytimeMinutes);
