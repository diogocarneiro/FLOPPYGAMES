using FloppyGames.Core.Startup;

namespace FloppyGames.Core.Tests.Startup;

public class AutostartManagerTests
{
    private const string ExecutablePath = @"C:\Program Files\FloppyGames\FloppyGames.Agent.exe";

    [Fact]
    public void IsEnabled_NoValueInRegistry_ReturnsFalse()
    {
        var manager = new AutostartManager(new FakeAutostartRegistry(), ExecutablePath);

        Assert.False(manager.IsEnabled);
    }

    [Fact]
    public void Enable_WritesQuotedExecutablePath_AndIsEnabledBecomesTrue()
    {
        var registry = new FakeAutostartRegistry();
        var manager = new AutostartManager(registry, ExecutablePath);

        manager.Enable();

        Assert.True(manager.IsEnabled);
        Assert.Equal($"\"{ExecutablePath}\"", registry.GetValue("FloppyGamesAgent"));
    }

    [Fact]
    public void Disable_RemovesValue_AndIsEnabledBecomesFalse()
    {
        var registry = new FakeAutostartRegistry();
        var manager = new AutostartManager(registry, ExecutablePath);
        manager.Enable();

        manager.Disable();

        Assert.False(manager.IsEnabled);
        Assert.Null(registry.GetValue("FloppyGamesAgent"));
    }

    [Fact]
    public void Disable_WhenNeverEnabled_DoesNotThrow()
    {
        var manager = new AutostartManager(new FakeAutostartRegistry(), ExecutablePath);

        var exception = Record.Exception(manager.Disable);

        Assert.Null(exception);
    }

    [Fact]
    public void Enable_DoesNotAffectOtherRegistryValues()
    {
        var registry = new FakeAutostartRegistry();
        registry.SetValue("SomeOtherApp", "\"C:\\other.exe\"");
        var manager = new AutostartManager(registry, ExecutablePath);

        manager.Enable();

        Assert.Equal("\"C:\\other.exe\"", registry.GetValue("SomeOtherApp"));
    }
}
