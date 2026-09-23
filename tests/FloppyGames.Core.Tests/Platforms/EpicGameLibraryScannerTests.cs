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

    private const string CatalogCache = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin";

    /// <summary>A cache real é JSON codificado em Base64 — uma lista de itens de catálogo com keyImages.</summary>
    private static string EncodeCatalog(string json) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

    private const string CatalogJson = """
        [
          { "id": "someOtherItem", "keyImages": [ { "type": "DieselGameBoxTall", "url": "https://cdn1.epicgames.com/other.jpg" } ] },
          { "id": "6fdb5feba66846cd8623a6d15ca68080", "title": "INSIDE", "keyImages": [
              { "type": "DieselGameBox", "url": "https://cdn1.epicgames.com/spt-assets/inside-wide.png" },
              { "type": "DieselGameBoxTall", "url": "https://cdn1.epicgames.com/13bb/item/INSIDE_340X440.jpg" } ] },
          { "id": "noTallImage", "keyImages": [ { "type": "DieselGameBox", "url": "https://cdn1.epicgames.com/wide.png" } ] }
        ]
        """;

    [Fact]
    public void TryResolveCoverUrl_ItemWithTallImage_ReturnsResizedUrl()
    {
        var fs = new FakeSteamFileSystem().WithFile(CatalogCache, EncodeCatalog(CatalogJson));
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir, CatalogCache);

        Assert.Equal(
            "https://cdn1.epicgames.com/13bb/item/INSIDE_340X440.jpg?h=800&resize=1&w=600&quality=medium",
            scanner.TryResolveCoverUrl("6fdb5feba66846cd8623a6d15ca68080"));
    }

    [Fact]
    public void TryResolveCoverUrl_ItemWithoutTallImage_ReturnsNull()
    {
        var fs = new FakeSteamFileSystem().WithFile(CatalogCache, EncodeCatalog(CatalogJson));
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir, CatalogCache);

        Assert.Null(scanner.TryResolveCoverUrl("noTallImage"));
    }

    [Fact]
    public void TryResolveCoverUrl_NoCatalogCache_ReturnsNull()
    {
        var scanner = new EpicGameLibraryScanner(new FakeSteamFileSystem(), ManifestsDir, CatalogCache);

        Assert.Null(scanner.TryResolveCoverUrl("6fdb5feba66846cd8623a6d15ca68080"));
    }

    [Fact]
    public void TryResolveCoverUrl_CorruptCatalogCache_ReturnsNull()
    {
        var fs = new FakeSteamFileSystem().WithFile(CatalogCache, "this is not base64 json!");
        var scanner = new EpicGameLibraryScanner(fs, ManifestsDir, CatalogCache);

        Assert.Null(scanner.TryResolveCoverUrl("6fdb5feba66846cd8623a6d15ca68080"));
    }
}
