using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

public class SteamLibraryScannerTests
{
    private const string PrimaryManifest = """
        "AppState"
        {
            "appid"        "730"
            "name"        "Counter-Strike 2"
            "installdir"        "Counter-Strike Global Offensive"
        }
        """;

    private const string SecondaryManifest = """
        "AppState"
        {
            "appid"        "400"
            "name"        "Portal"
            "installdir"        "Portal"
        }
        """;

    [Fact]
    public void ScanInstalledGames_NoSteamInstalled_ReturnsEmpty()
    {
        var scanner = new SteamLibraryScanner(new FakeSteamFileSystem(), new FakeSteamPathProvider(null));

        var games = scanner.ScanInstalledGames();

        Assert.Empty(games);
    }

    [Fact]
    public void ScanInstalledGames_SingleLibraryNoVdf_ReadsGamesFromPrimaryLibrary()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\appmanifest_730.acf", PrimaryManifest);
        var scanner = new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam"));

        var games = scanner.ScanInstalledGames();

        var game = Assert.Single(games);
        Assert.Equal(730, game.AppId);
        Assert.Equal("Counter-Strike 2", game.Name);
        Assert.Equal(@"C:\Steam\steamapps\common\Counter-Strike Global Offensive", game.InstallPath);
        Assert.Null(game.SizeOnDiskBytes);
    }

    [Fact]
    public void ScanInstalledGames_ManifestWithSizeOnDisk_ParsesIt()
    {
        const string manifestWithSize = """
            "AppState"
            {
                "appid"        "730"
                "name"        "Counter-Strike 2"
                "installdir"        "Counter-Strike Global Offensive"
                "SizeOnDisk"        "68719476736"
            }
            """;

        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\appmanifest_730.acf", manifestWithSize);
        var scanner = new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam"));

        var games = scanner.ScanInstalledGames();

        var game = Assert.Single(games);
        Assert.Equal(68719476736L, game.SizeOnDiskBytes);
    }

    [Fact]
    public void ScanInstalledGames_MultipleLibraryFolders_ScansAllOfThem()
    {
        const string librariesVdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"        "C:\\Steam"
                }
                "1"
                {
                    "path"        "D:\\SteamLibrary"
                }
            }
            """;

        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\libraryfolders.vdf", librariesVdf)
            .WithFile(@"C:\Steam\steamapps\appmanifest_730.acf", PrimaryManifest)
            .WithFile(@"D:\SteamLibrary\steamapps\appmanifest_400.acf", SecondaryManifest);
        var scanner = new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam"));

        var games = scanner.ScanInstalledGames();

        Assert.Equal(2, games.Count);
        Assert.Contains(games, g => g.AppId == 730);
        Assert.Contains(games, g => g.AppId == 400);
    }

    [Fact]
    public void ScanInstalledGames_ResultsSortedAlphabeticallyByName()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\appmanifest_730.acf", PrimaryManifest)
            .WithFile(@"C:\Steam\steamapps\appmanifest_400.acf", SecondaryManifest);
        var scanner = new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam"));

        var games = scanner.ScanInstalledGames();

        Assert.Equal(["Counter-Strike 2", "Portal"], games.Select(g => g.Name));
    }

    [Fact]
    public void ScanInstalledGames_CorruptManifest_SkipsItButKeepsOthers()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\appmanifest_730.acf", PrimaryManifest)
            .WithFile(@"C:\Steam\steamapps\appmanifest_999.acf", "not valid vdf {{{");
        var scanner = new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam"));

        var games = scanner.ScanInstalledGames();

        var game = Assert.Single(games);
        Assert.Equal(730, game.AppId);
    }

    [Fact]
    public void ScanInstalledGames_CorruptLibraryFoldersVdf_FallsBackToPrimaryLibraryOnly()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\steamapps\libraryfolders.vdf", "not valid vdf {{{")
            .WithFile(@"C:\Steam\steamapps\appmanifest_730.acf", PrimaryManifest);
        var scanner = new SteamLibraryScanner(fs, new FakeSteamPathProvider(@"C:\Steam"));

        var games = scanner.ScanInstalledGames();

        var game = Assert.Single(games);
        Assert.Equal(730, game.AppId);
    }
}
