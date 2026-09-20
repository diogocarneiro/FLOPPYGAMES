using FloppyGames.Core.Configuration;
using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

public class FloppyMediaWriterTests
{
    private const string DriveRoot = "E:\\";

    private static readonly GameConfig Config = new()
    {
        Title = "Portal",
        AppId = 400,
        Process = "portal.exe",
    };

    [Fact]
    public void Check_NotRemovableDrive_ReturnsBlocked()
    {
        var inspector = new FakeDriveInspector().WithFixedDrive(DriveRoot);
        var writer = new FloppyMediaWriter(inspector);

        var result = writer.Check(DriveRoot, Config, coverBytes: null);

        Assert.Equal(MediaWriteCheckStatus.Blocked, result.Status);
    }

    [Fact]
    public void Check_InsufficientSpace_ReturnsBlocked()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithFreeBytes(DriveRoot, 10);
        var writer = new FloppyMediaWriter(inspector);

        var result = writer.Check(DriveRoot, Config, coverBytes: new byte[1000]);

        Assert.Equal(MediaWriteCheckStatus.Blocked, result.Status);
        Assert.Contains("Espaço insuficiente", result.Message);
    }

    [Fact]
    public void Check_EnoughSpaceNoExistingGameIni_ReturnsReady()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithFreeBytes(DriveRoot, 1_000_000);
        var writer = new FloppyMediaWriter(inspector);

        var result = writer.Check(DriveRoot, Config, coverBytes: null);

        Assert.Equal(MediaWriteCheckStatus.Ready, result.Status);
    }

    [Fact]
    public void Check_ExistingGameIni_ReturnsNeedsConfirmation()
    {
        var inspector = new FakeDriveInspector()
            .WithRemovableDrive(DriveRoot)
            .WithFreeBytes(DriveRoot, 1_000_000)
            .WithGameIni(DriveRoot, "[Game]\nTITLE=Old\nAPPID=1\nPROCESS=old.exe");
        var writer = new FloppyMediaWriter(inspector);

        var result = writer.Check(DriveRoot, Config, coverBytes: null);

        Assert.Equal(MediaWriteCheckStatus.NeedsConfirmation, result.Status);
    }
}
