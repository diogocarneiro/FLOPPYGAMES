using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

public class GameMediaScannerTests
{
    private const string DriveRoot = "E:\\";

    private const string ValidIni = """
        [Game]
        TITLE=Portal
        APPID=400
        PROCESS=portal.exe
        """;

    [Fact]
    public void Scan_FixedDrive_ReturnsNotRemovable()
    {
        var inspector = new FakeDriveInspector().WithFixedDrive(DriveRoot).WithGameIni(DriveRoot, ValidIni);
        var scanner = new GameMediaScanner(inspector);

        var result = scanner.Scan(DriveRoot);

        Assert.Equal(GameMediaScanStatus.NotRemovable, result.Status);
        Assert.Null(result.Config);
    }

    [Fact]
    public void Scan_RemovableDriveWithoutGameIni_ReturnsNoGameIni()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot);
        var scanner = new GameMediaScanner(inspector);

        var result = scanner.Scan(DriveRoot);

        Assert.Equal(GameMediaScanStatus.NoGameIni, result.Status);
    }

    [Fact]
    public void Scan_RemovableDriveWithInvalidGameIni_ReturnsInvalidGameIniWithErrors()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithGameIni(DriveRoot, "[Game]\nTITLE=Sem AppID");
        var scanner = new GameMediaScanner(inspector);

        var result = scanner.Scan(DriveRoot);

        Assert.Equal(GameMediaScanStatus.InvalidGameIni, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Scan_RemovableDriveWithValidGameIni_ReturnsValidWithConfig()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithGameIni(DriveRoot, ValidIni);
        var scanner = new GameMediaScanner(inspector);

        var result = scanner.Scan(DriveRoot);

        Assert.Equal(GameMediaScanStatus.Valid, result.Status);
        Assert.Equal("Portal", result.Config!.Title);
        Assert.Equal(400, result.Config.AppId);
    }
}
