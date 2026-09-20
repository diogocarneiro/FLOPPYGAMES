using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Media;

public enum GameMediaScanStatus
{
    Valid,
    NotRemovable,
    NoGameIni,
    InvalidGameIni,
}

/// <summary>Resultado da inspeção de uma unidade recém-detetada.</summary>
public sealed record GameMediaScanResult
{
    public required string DriveRoot { get; init; }

    public required GameMediaScanStatus Status { get; init; }

    public GameConfig? Config { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static GameMediaScanResult Valid(string driveRoot, GameConfig config) =>
        new() { DriveRoot = driveRoot, Status = GameMediaScanStatus.Valid, Config = config };

    public static GameMediaScanResult NotRemovable(string driveRoot) =>
        new() { DriveRoot = driveRoot, Status = GameMediaScanStatus.NotRemovable };

    public static GameMediaScanResult NoGameIni(string driveRoot) =>
        new() { DriveRoot = driveRoot, Status = GameMediaScanStatus.NoGameIni };

    public static GameMediaScanResult InvalidGameIni(string driveRoot, IReadOnlyList<string> errors) =>
        new() { DriveRoot = driveRoot, Status = GameMediaScanStatus.InvalidGameIni, Errors = errors };
}
