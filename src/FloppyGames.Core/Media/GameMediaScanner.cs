using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Media;

/// <summary>
/// Decide se uma unidade recém-detetada é um suporte FloppyGames válido:
/// tem de ser amovível e conter um GAME.INI bem formado.
/// </summary>
public sealed class GameMediaScanner
{
    private readonly IRemovableDriveInspector _inspector;

    public GameMediaScanner(IRemovableDriveInspector inspector)
    {
        _inspector = inspector;
    }

    public GameMediaScanResult Scan(string driveRoot)
    {
        if (!_inspector.IsRemovableDrive(driveRoot))
        {
            return GameMediaScanResult.NotRemovable(driveRoot);
        }

        if (!_inspector.TryReadGameIni(driveRoot, out var content) || content is null)
        {
            return GameMediaScanResult.NoGameIni(driveRoot);
        }

        var parseResult = GameIniParser.Parse(content);

        return parseResult.Success
            ? GameMediaScanResult.Valid(driveRoot, parseResult.Config!)
            : GameMediaScanResult.InvalidGameIni(driveRoot, parseResult.Errors);
    }
}
