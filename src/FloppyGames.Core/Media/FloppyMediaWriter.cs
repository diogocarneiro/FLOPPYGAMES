using System.Text;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Localization;

namespace FloppyGames.Core.Media;

/// <summary>
/// Escreve um <see cref="GameConfig"/> (GAME.INI + capa opcional) para um suporte amovível,
/// validando primeiro que a unidade é amovível, tem espaço, e avisando antes de sobrescrever
/// um GAME.INI já existente.
/// </summary>
public sealed class FloppyMediaWriter
{
    private const int WriteChunkSize = 16 * 1024;

    private readonly IRemovableDriveInspector _inspector;

    public FloppyMediaWriter(IRemovableDriveInspector inspector) => _inspector = inspector;

    public MediaWriteCheck Check(string driveRoot, GameConfig config, byte[]? coverBytes)
    {
        if (!_inspector.IsRemovableDrive(driveRoot))
        {
            return MediaWriteCheck.Blocked(Strings.Core_MediaWriter_NotRemovable);
        }

        var iniBytes = Encoding.UTF8.GetByteCount(GameIniWriter.Write(config));
        var requiredBytes = iniBytes + (coverBytes?.Length ?? 0);
        var freeBytes = _inspector.GetAvailableFreeBytes(driveRoot);

        if (freeBytes < requiredBytes)
        {
            return MediaWriteCheck.Blocked(
                Strings.Core_MediaWriter_InsufficientSpace(FormatBytes(requiredBytes), FormatBytes(freeBytes)));
        }

        return _inspector.TryReadGameIni(driveRoot, out _)
            ? MediaWriteCheck.NeedsConfirmation(Strings.Core_MediaWriter_ExistingGameIni)
            : MediaWriteCheck.Ready();
    }

    /// <summary>
    /// Escreve de facto para o suporte. Chamar só depois de <see cref="Check"/> não devolver
    /// <c>Blocked</c>. Numa disquete real (~30-60 KB/s) uma capa pode demorar vários segundos a
    /// gravar, por isso a escrita é feita em pedaços com <see cref="FileOptions.WriteThrough"/> e
    /// reporta <paramref name="progress"/> depois de cada pedaço — assim o progresso acompanha o que
    /// já está de facto no suporte, não só o que ficou na cache de escrita do Windows. Nunca chamar
    /// na thread de UI.
    /// </summary>
    public void Write(
        string driveRoot, GameConfig config, byte[]? coverBytes, string? coverFileName,
        IProgress<MediaWriteProgress>? progress = null)
    {
        var iniBytes = Encoding.UTF8.GetBytes(GameIniWriter.Write(config));
        var writeCover = coverBytes is not null && !string.IsNullOrWhiteSpace(coverFileName);
        long totalBytes = iniBytes.Length + (writeCover ? coverBytes!.Length : 0);
        long bytesWritten = 0;

        WriteFileInChunks(Path.Combine(driveRoot, "GAME.INI"), iniBytes, totalBytes, ref bytesWritten, progress);

        if (writeCover)
        {
            WriteFileInChunks(Path.Combine(driveRoot, coverFileName!), coverBytes!, totalBytes, ref bytesWritten, progress);
        }
    }

    private static void WriteFileInChunks(
        string path, byte[] content, long totalBytes, ref long bytesWritten, IProgress<MediaWriteProgress>? progress)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, WriteChunkSize, FileOptions.WriteThrough);

        for (var offset = 0; offset < content.Length; offset += WriteChunkSize)
        {
            var count = Math.Min(WriteChunkSize, content.Length - offset);
            stream.Write(content, offset, count);
            stream.Flush();
            bytesWritten += count;
            progress?.Report(new MediaWriteProgress(bytesWritten, totalBytes));
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} bytes",
    };
}
