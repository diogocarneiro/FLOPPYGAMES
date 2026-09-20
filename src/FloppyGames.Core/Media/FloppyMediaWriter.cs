using System.Text;
using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Media;

/// <summary>
/// Escreve um <see cref="GameConfig"/> (GAME.INI + capa opcional) para um suporte amovível,
/// validando primeiro que a unidade é amovível, tem espaço, e avisando antes de sobrescrever
/// um GAME.INI já existente.
/// </summary>
public sealed class FloppyMediaWriter
{
    private readonly IRemovableDriveInspector _inspector;

    public FloppyMediaWriter(IRemovableDriveInspector inspector) => _inspector = inspector;

    public MediaWriteCheck Check(string driveRoot, GameConfig config, byte[]? coverBytes)
    {
        if (!_inspector.IsRemovableDrive(driveRoot))
        {
            return MediaWriteCheck.Blocked("A unidade selecionada não é amovível.");
        }

        var iniBytes = Encoding.UTF8.GetByteCount(GameIniWriter.Write(config));
        var requiredBytes = iniBytes + (coverBytes?.Length ?? 0);
        var freeBytes = _inspector.GetAvailableFreeBytes(driveRoot);

        if (freeBytes < requiredBytes)
        {
            return MediaWriteCheck.Blocked(
                $"Espaço insuficiente: são precisos {FormatBytes(requiredBytes)}, há {FormatBytes(freeBytes)} livres.");
        }

        return _inspector.TryReadGameIni(driveRoot, out _)
            ? MediaWriteCheck.NeedsConfirmation("Este suporte já tem um GAME.INI — escrever vai substituí-lo.")
            : MediaWriteCheck.Ready();
    }

    /// <summary>Escreve de facto para o suporte. Chamar só depois de <see cref="Check"/> não devolver <c>Blocked</c>.</summary>
    public void Write(string driveRoot, GameConfig config, byte[]? coverBytes, string? coverFileName)
    {
        File.WriteAllText(Path.Combine(driveRoot, "GAME.INI"), GameIniWriter.Write(config));

        if (coverBytes is not null && !string.IsNullOrWhiteSpace(coverFileName))
        {
            File.WriteAllBytes(Path.Combine(driveRoot, coverFileName), coverBytes);
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} bytes",
    };
}
