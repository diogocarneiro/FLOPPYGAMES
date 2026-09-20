using FloppyGames.Core.Media;
using Serilog.Core;

namespace FloppyGames.Core.Tests.Media;

public class RemovableGameMediaServiceTests
{
    private const string DriveRoot = "E:\\";

    private const string ValidIni = """
        [Game]
        TITLE=Portal
        APPID=400
        PROCESS=portal.exe
        """;

    private static (FakeRemovableMediaWatcher Watcher, RemovableGameMediaService Service) CreateService(FakeDriveInspector inspector)
    {
        var watcher = new FakeRemovableMediaWatcher();
        var scanner = new GameMediaScanner(inspector);
        var service = new RemovableGameMediaService(watcher, scanner, Logger.None);
        return (watcher, service);
    }

    [Fact]
    public void Start_DelegatesToUnderlyingWatcher()
    {
        var (watcher, service) = CreateService(new FakeDriveInspector());

        service.Start();

        Assert.Equal(1, watcher.StartCalls);
    }

    [Fact]
    public void DriveArrived_WithValidGameIni_RaisesMediaInserted()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithGameIni(DriveRoot, ValidIni);
        var (watcher, service) = CreateService(inspector);

        MediaInsertedEventArgs? received = null;
        service.MediaInserted += (_, e) => received = e;

        watcher.RaiseArrived(DriveRoot);

        Assert.NotNull(received);
        Assert.Equal(DriveRoot, received!.DriveRoot);
        Assert.Equal("Portal", received.Config.Title);
    }

    [Fact]
    public void DriveArrived_WithInvalidGameIni_RaisesInvalidMediaDetectedNotMediaInserted()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithGameIni(DriveRoot, "[Game]\nTITLE=Sem AppID");
        var (watcher, service) = CreateService(inspector);

        var insertedRaised = false;
        InvalidMediaEventArgs? invalid = null;
        service.MediaInserted += (_, _) => insertedRaised = true;
        service.InvalidMediaDetected += (_, e) => invalid = e;

        watcher.RaiseArrived(DriveRoot);

        Assert.False(insertedRaised);
        Assert.NotNull(invalid);
        Assert.NotEmpty(invalid!.Errors);
    }

    [Fact]
    public void DriveArrived_ThenRemoved_RaisesMediaRemovedWithSameConfig()
    {
        var inspector = new FakeDriveInspector().WithRemovableDrive(DriveRoot).WithGameIni(DriveRoot, ValidIni);
        var (watcher, service) = CreateService(inspector);

        MediaRemovedEventArgs? removed = null;
        service.MediaRemoved += (_, e) => removed = e;

        watcher.RaiseArrived(DriveRoot);
        watcher.RaiseRemoved(DriveRoot);

        Assert.NotNull(removed);
        Assert.Equal(DriveRoot, removed!.DriveRoot);
        Assert.Equal(400, removed.Config.AppId);
    }

    [Fact]
    public void DriveRemoved_ForUntrackedDrive_DoesNotRaiseMediaRemoved()
    {
        var (watcher, service) = CreateService(new FakeDriveInspector());

        var removedRaised = false;
        service.MediaRemoved += (_, _) => removedRaised = true;

        watcher.RaiseRemoved(DriveRoot);

        Assert.False(removedRaised);
    }

    [Fact]
    public void DriveArrived_ForFixedDrive_RaisesNoEvents()
    {
        var inspector = new FakeDriveInspector().WithFixedDrive(DriveRoot);
        var (watcher, service) = CreateService(inspector);

        var anyEventRaised = false;
        service.MediaInserted += (_, _) => anyEventRaised = true;
        service.InvalidMediaDetected += (_, _) => anyEventRaised = true;

        watcher.RaiseArrived(DriveRoot);

        Assert.False(anyEventRaised);
    }
}
