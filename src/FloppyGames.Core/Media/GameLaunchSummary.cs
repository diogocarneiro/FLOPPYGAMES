using FloppyGames.Core.Configuration;
using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Media;

/// <summary>
/// Dados adicionais sobre o lançamento em curso, apurados a partir do próprio suporte e da
/// biblioteca local da plataforma, para o ecrã de arranque mostrar. Os campos a partir de
/// <see cref="BuildId"/> são exclusivos da Steam — sem equivalente local fiável na Epic/GOG,
/// ficam sempre <c>null</c> nas outras plataformas. <see cref="Achievements"/> só vem preenchido
/// se houver uma chave da Steam Web API configurada.
/// </summary>
public sealed record GameLaunchSummary(
    GamePlatform Platform,
    MediaKind MediaKind,
    long MediaSizeBytes,
    bool IsInstalled,
    long? InstalledSizeBytes,
    string? BuildId,
    DateTime? LastUpdatedUtc,
    DateTime? LastPlayedUtc,
    long? PlaytimeMinutes,
    AchievementSummary? Achievements);
