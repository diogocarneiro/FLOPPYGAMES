using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

public class CompositeRemovableMediaWatcherTests
{
    [Fact]
    public void Start_StartsAllInnerWatchers()
    {
        var a = new FakeRemovableMediaWatcher();
        var b = new FakeRemovableMediaWatcher();
        var composite = new CompositeRemovableMediaWatcher([a, b]);

        composite.Start();

        Assert.Equal(1, a.StartCalls);
        Assert.Equal(1, b.StartCalls);
    }

    [Fact]
    public void Stop_StopsAllInnerWatchers()
    {
        var a = new FakeRemovableMediaWatcher();
        var b = new FakeRemovableMediaWatcher();
        var composite = new CompositeRemovableMediaWatcher([a, b]);

        composite.Stop();

        Assert.Equal(1, a.StopCalls);
        Assert.Equal(1, b.StopCalls);
    }

    [Fact]
    public void DriveArrived_FromAnyInnerWatcher_IsForwardedWithOriginalDrive()
    {
        var a = new FakeRemovableMediaWatcher();
        var b = new FakeRemovableMediaWatcher();
        var composite = new CompositeRemovableMediaWatcher([a, b]);

        var received = new List<string>();
        composite.DriveArrived += (_, root) => received.Add(root);

        a.RaiseArrived("E:\\");
        b.RaiseArrived("A:\\");

        Assert.Equal(["E:\\", "A:\\"], received);
    }

    [Fact]
    public void DriveRemoved_FromAnyInnerWatcher_IsForwarded()
    {
        var a = new FakeRemovableMediaWatcher();
        var b = new FakeRemovableMediaWatcher();
        var composite = new CompositeRemovableMediaWatcher([a, b]);

        var received = new List<string>();
        composite.DriveRemoved += (_, root) => received.Add(root);

        b.RaiseRemoved("A:\\");

        Assert.Equal(["A:\\"], received);
    }
}
