using FloppyGames.Core.Configuration;
using FloppyGames.Core.Platforms;
using FloppyGames.Core.Tests.Steam;

namespace FloppyGames.Core.Tests.Platforms;

public class EpicGameLibraryScannerTests
{
    private const string ManifestsDir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";

    private const string InsideManifest = """
        {
            "DisplayName": "INSIDE",
            "InstallLocation": "C:\\Program Files\\Epic Games\\Inside",
            "InstallSize": 2246081768,
            "CatalogNamespace": "13bb5776b9e1424d84ce42d9ba61c0ca",
            "CatalogItemId": "6fdb5feba66846cd8623a6d15ca68080",
            "AppName": "Marigold"
        }
        """;

    [Fact]
    public void ScanInstalledGames_NoManifestsDirectory_ReturnsEmpty()
    {
        var scanner = new EpicGameLibraryScanner(new FakeSteamFileSystem(), ManifestsDir);

        Assert.Empty(scanner.ScanInstalledGames());
    }

    [Fact]
    public void ScanInstalledGames_ValidManifest_MapsAllFields()
    {
        var fs = new FakeSteamFileSystem().WithFile(Path.Combine(ManifestsDir, "game.item"), InsideManifest);
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir);

        var game = Assert.Single(scanner.ScanInstalledGames());

        Assert.Equal("INSIDE", game.Name);
        Assert.Equal(GamePlatform.Epic, game.Platform);
        Assert.Equal(@"C:\Program Files\Epic Games\Inside", game.InstallPath);
        Assert.Equal(2246081768, game.InstalledSizeBytes);
        Assert.Equal("13bb5776b9e1424d84ce42d9ba61c0ca", game.EpicNamespace);
        Assert.Equal("6fdb5feba66846cd8623a6d15ca68080", game.EpicItemId);
        Assert.Equal("Marigold", game.EpicAppName);
    }

    [Fact]
    public void ScanInstalledGames_OnlyReadsItemFiles()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(Path.Combine(ManifestsDir, "game.item"), InsideManifest)
            .WithFile(Path.Combine(ManifestsDir, "game.mancpn"), "{}");
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir);

        Assert.Single(scanner.ScanInstalledGames());
    }

    [Fact]
    public void ScanInstalledGames_CorruptManifest_SkipsItButKeepsOthers()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(Path.Combine(ManifestsDir, "good.item"), InsideManifest)
            .WithFile(Path.Combine(ManifestsDir, "bad.item"), "not valid json {{{");
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir);

        var game = Assert.Single(scanner.ScanInstalledGames());
        Assert.Equal("INSIDE", game.Name);
    }

    [Fact]
    public void ScanInstalledGames_ManifestMissingRequiredField_IsSkipped()
    {
        const string incomplete = """
            {
                "DisplayName": "INSIDE",
                "InstallLocation": "C:\\Program Files\\Epic Games\\Inside"
            }
            """;
        var fs = new FakeSteamFileSystem().WithFile(Path.Combine(ManifestsDir, "game.item"), incomplete);
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir);

        Assert.Empty(scanner.ScanInstalledGames());
    }

    [Fact]
    public void ScanInstalledGames_ResultsSortedAlphabeticallyByName()
    {
        const string zManifest = """
            {
                "DisplayName": "Zebra",
                "InstallLocation": "C:\\Games\\Zebra",
                "CatalogNamespace": "ns",
                "CatalogItemId": "z",
                "AppName": "Zebra"
            }
            """;
        var fs = new FakeSteamFileSystem()
            .WithFile(Path.Combine(ManifestsDir, "inside.item"), InsideManifest)
            .WithFile(Path.Combine(ManifestsDir, "zebra.item"), zManifest);
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir);

        Assert.Equal(["INSIDE", "Zebra"], scanner.ScanInstalledGames().Select(g => g.Name));
    }
}
