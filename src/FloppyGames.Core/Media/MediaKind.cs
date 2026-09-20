namespace FloppyGames.Core.Media;

/// <summary>O tipo físico de suporte amovível que despoletou o lançamento de um jogo.</summary>
public enum MediaKind
{
    Floppy,
    Usb,
}

/// <summary>
/// Classifica uma unidade pela sua letra: as disquetes só podem estar nas letras vigiadas por
/// <see cref="PollingFloppyDriveWatcher"/> (A:\ e B:\, por convenção do BIOS/Windows há décadas);
/// qualquer outra letra amovível é uma pen USB.
/// </summary>
public static class MediaKindClassifier
{
    public static MediaKind Classify(string driveRoot) =>
        PollingFloppyDriveWatcher.DefaultCandidateDriveRoots.Contains(driveRoot, StringComparer.OrdinalIgnoreCase)
            ? MediaKind.Floppy
            : MediaKind.Usb;
}
