using FloppyGames.Core.Catalog;
using FloppyGames.Core.Configuration;
using FloppyGames.Core.Platforms;

namespace FloppyGames.Core.Tests.Catalog;

public class GameCatalogTests : IDisposable
{
    private readonly string _catalogDir;
    private readonly string _catalogPath;

    public GameCatalogTests()
    {
        _catalogDir = Path.Combine(Path.GetTempPath(), "FloppyGamesTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_catalogDir);
        _catalogPath = Path.Combine(_catalogDir, "catalog.json");
    }

    public void Dispose() => Directory.Delete(_catalogDir, recursive: true);

    private const string SampleCatalogJson = """
        [
          {
            "Platform": "Steam",
            "SteamAppId": 730,
            "Title": "Counter-Strike 2",
            "Process": "cs2.exe",
            "Description": { "en": "Competitive FPS.", "pt": "FPS competitivo." },
            "Cover": null
          },
          {
            "Platform": "Epic",
            "EpicNamespace": "ns",
            "EpicItemId": "item",
            "EpicAppName": "app",
            "Title": "INSIDE",
            "Process": "INSIDE.exe",
            "Description": { "en": "A dark puzzle-platformer." },
            "Cover": "inside.jpg"
          }
        ]
        """;

    [Fact]
    public void Entries_FileDoesNotExist_ReturnsEmpty()
    {
        var catalog = new GameCatalog(Path.Combine(_catalogDir, "does-not-exist.json"));

        Assert.Empty(catalog.Entries);
    }

    [Fact]
    public void Entries_MalformedJson_ReturnsEmptyInsteadOfThrowing()
    {
        File.WriteAllText(_catalogPath, "not valid json {{{");
        var catalog = new GameCatalog(_catalogPath);

        Assert.Empty(catalog.Entries);
    }

    [Fact]
    public void Entries_ValidCatalog_ParsesPlatformEnumFromString()
    {
        File.WriteAllText(_catalogPath, SampleCatalogJson);
        var catalog = new GameCatalog(_catalogPath);

        Assert.Equal(2, catalog.Entries.Count);
        Assert.Contains(catalog.Entries, e => e.Platform == GamePlatform.Steam && e.SteamAppId == 730);
        Assert.Contains(catalog.Entries, e => e.Platform == GamePlatform.Epic && e.EpicItemId == "item");
    }

    [Fact]
    public void TryFind_SteamGameMatchingAppId_ReturnsEntry()
    {
        File.WriteAllText(_catalogPath, SampleCatalogJson);
        var catalog = new GameCatalog(_catalogPath);
        var game = new DiscoveredGame("Counter-Strike 2", GamePlatform.Steam, @"C:\Games\CS2", SteamAppId: 730);

        var entry = catalog.TryFind(game);

        Assert.NotNull(entry);
        Assert.Equal("cs2.exe", entry!.Process);
    }

    [Fact]
    public void TryFind_EpicGameMatchingItemId_ReturnsEntry()
    {
        File.WriteAllText(_catalogPath, SampleCatalogJson);
        var catalog = new GameCatalog(_catalogPath);
        var game = new DiscoveredGame(
            "INSIDE", GamePlatform.Epic, @"C:\Games\Inside", EpicNamespace: "ns", EpicItemId: "item", EpicAppName: "app");

        var entry = catalog.TryFind(game);

        Assert.NotNull(entry);
        Assert.Equal("INSIDE.exe", entry!.Process);
    }

    [Fact]
    public void TryFind_NoMatch_ReturnsNull()
    {
        File.WriteAllText(_catalogPath, SampleCatalogJson);
        var catalog = new GameCatalog(_catalogPath);
        var game = new DiscoveredGame("Some Other Game", GamePlatform.Steam, @"C:\Games\Other", SteamAppId: 999999);

        Assert.Null(catalog.TryFind(game));
    }

    [Fact]
    public void ResolveCoverPath_EntryWithoutCover_ReturnsNull()
    {
        File.WriteAllText(_catalogPath, SampleCatalogJson);
        var catalog = new GameCatalog(_catalogPath);
        var entry = catalog.Entries.Single(e => e.Platform == GamePlatform.Steam);

        Assert.Null(catalog.ResolveCoverPath(entry));
    }

    [Fact]
    public void ResolveCoverPath_EntryWithCover_ReturnsPathUnderCoversFolder()
    {
        File.WriteAllText(_catalogPath, SampleCatalogJson);
        var catalog = new GameCatalog(_catalogPath);
        var entry = catalog.Entries.Single(e => e.Platform == GamePlatform.Epic);

        var coverPath = catalog.ResolveCoverPath(entry);

        Assert.Equal(Path.Combine(_catalogDir, "covers", "inside.jpg"), coverPath);
    }
}
