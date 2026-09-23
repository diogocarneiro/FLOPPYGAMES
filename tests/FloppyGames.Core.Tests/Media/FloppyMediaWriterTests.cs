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

    [Fact]
    public void Write_WritesGameIniWithoutBomAndCoverExactly()
    {
        var root = CreateTempDriveRoot();
        try
        {
            var cover = Enumerable.Range(0, 40_000).Select(i => (byte)i).ToArray();
            var writer = new FloppyMediaWriter(new FakeDriveInspector());

            writer.Write(root, Config, cover, "cover.jpg");

            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(GameIniWriter.Write(Config)), File.ReadAllBytes(Path.Combine(root, "GAME.INI")));
            Assert.Equal(cover, File.ReadAllBytes(Path.Combine(root, "cover.jpg")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_ReportsIncreasingProgressEndingAtTotal()
    {
        var root = CreateTempDriveRoot();
        try
        {
            var cover = new byte[40_000];
            var writer = new FloppyMediaWriter(new FakeDriveInspector());
            var reports = new List<MediaWriteProgress>();

            writer.Write(root, Config, cover, "cover.jpg", new SynchronousProgress<MediaWriteProgress>(reports.Add));

            var expectedTotal = System.Text.Encoding.UTF8.GetByteCount(GameIniWriter.Write(Config)) + cover.Length;
            Assert.True(reports.Count > 2, "a capa de 40 KB deve ser escrita em vários pedaços");
            Assert.All(reports, r => Assert.Equal(expectedTotal, r.TotalBytes));
            Assert.Equal(reports.Select(r => r.BytesWritten).OrderBy(b => b), reports.Select(r => r.BytesWritten));
            Assert.Equal(expectedTotal, reports[^1].BytesWritten);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDriveRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "FloppyGamesTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }
}
