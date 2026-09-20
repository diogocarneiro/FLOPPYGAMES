using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

public class ExecutableSuggesterTests
{
    [Fact]
    public void Suggest_NoExecutables_ReturnsNull()
    {
        var result = ExecutableSuggester.Suggest([], "SomeGame");

        Assert.Null(result);
    }

    [Fact]
    public void Suggest_ExecutableMatchingInstallDirectoryName_IsPreferred()
    {
        var result = ExecutableSuggester.Suggest(
            ["UnityCrashHandler64.exe", "cs2.exe", "steamclient64.exe"],
            "cs2");

        Assert.Equal("cs2.exe", result);
    }

    [Fact]
    public void Suggest_IgnoresKnownInstallerAndRedistributableNames()
    {
        var result = ExecutableSuggester.Suggest(
            ["UnityCrashHandler64.exe", "vcredist_x64.exe", "unins000.exe", "Portal.exe"],
            "Portal");

        Assert.Equal("Portal.exe", result);
    }

    [Fact]
    public void Suggest_NoNameMatch_PrefersShortestFileName()
    {
        var result = ExecutableSuggester.Suggest(
            ["some_long_launcher_wrapper.exe", "hl2.exe"],
            "Half-Life 2");

        Assert.Equal("hl2.exe", result);
    }

    [Fact]
    public void Suggest_AllCandidatesAreIgnoredPatterns_FallsBackToOriginalList()
    {
        var result = ExecutableSuggester.Suggest(["setup.exe", "installer.exe"], "SomeGame");

        Assert.NotNull(result);
    }
}
