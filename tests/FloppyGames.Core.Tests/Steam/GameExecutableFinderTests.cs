using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

public class GameExecutableFinderTests
{
    [Fact]
    public void FindSuggestedExecutable_InstallPathMissing_ReturnsNull()
    {
        var finder = new GameExecutableFinder(new FakeSteamFileSystem());

        var result = finder.FindSuggestedExecutable(@"C:\Steam\steamapps\common\Missing");

        Assert.Null(result);
    }

    [Fact]
    public void FindSuggestedExecutable_ScansRecursivelyAndSuggestsBestMatch()
    {
        var fs = new FakeSteamFileSystem()
            .WithDirectory(@"C:\Steam\steamapps\common\Portal")
            .WithFile(@"C:\Steam\steamapps\common\Portal\bin\UnityCrashHandler64.exe", string.Empty)
            .WithFile(@"C:\Steam\steamapps\common\Portal\bin\Portal.exe", string.Empty);
        var finder = new GameExecutableFinder(fs);

        var result = finder.FindSuggestedExecutable(@"C:\Steam\steamapps\common\Portal");

        Assert.Equal("Portal.exe", result);
    }
}
