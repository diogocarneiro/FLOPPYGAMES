using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

public class SteamPlaytimeReaderTests
{
    private const ulong OwnerSteamId64 = 76561197960265728UL + 12345UL;

    private const string LocalConfigWithPortal = """
        "UserLocalConfigStore"
        {
            "Software"
            {
                "Valve"
                {
                    "Steam"
                    {
                        "apps"
                        {
                            "400"
                            {
                                "LastPlayed"        "1690000000"
                                "Playtime"        "125"
                            }
                        }
                    }
                }
            }
        }
        """;

    [Fact]
    public void TryGetPlaytimeMinutes_NoSteamInstalled_ReturnsNull()
    {
        var reader = new SteamPlaytimeReader(new FakeSteamFileSystem(), new FakeSteamPathProvider(null));

        Assert.Null(reader.TryGetPlaytimeMinutes(400, OwnerSteamId64));
    }

    [Fact]
    public void TryGetPlaytimeMinutes_KnownOwnerAndApp_DerivesSteamId3AndReadsPlaytime()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\userdata\12345\config\localconfig.vdf", LocalConfigWithPortal);
        var reader = new SteamPlaytimeReader(fs, new FakeSteamPathProvider(@"C:\Steam"));

        Assert.Equal(125, reader.TryGetPlaytimeMinutes(400, OwnerSteamId64));
    }

    [Fact]
    public void TryGetPlaytimeMinutes_AppNotInLocalConfig_ReturnsNull()
    {
        var fs = new FakeSteamFileSystem()
            .WithFile(@"C:\Steam\userdata\12345\config\localconfig.vdf", LocalConfigWithPortal);
        var reader = new SteamPlaytimeReader(fs, new FakeSteamPathProvider(@"C:\Steam"));

        Assert.Null(reader.TryGetPlaytimeMinutes(999, OwnerSteamId64));
    }

    [Fact]
    public void TryGetPlaytimeMinutes_LocalConfigMissing_ReturnsNull()
    {
        var reader = new SteamPlaytimeReader(new FakeSteamFileSystem(), new FakeSteamPathProvider(@"C:\Steam"));

        Assert.Null(reader.TryGetPlaytimeMinutes(400, OwnerSteamId64));
    }

    [Fact]
    public void TryGetPlaytimeMinutes_OwnerBelowIndividualAccountBase_ReturnsNull()
    {
        var reader = new SteamPlaytimeReader(new FakeSteamFileSystem(), new FakeSteamPathProvider(@"C:\Steam"));

        Assert.Null(reader.TryGetPlaytimeMinutes(400, ownerSteamId64: 12345UL));
    }
}
