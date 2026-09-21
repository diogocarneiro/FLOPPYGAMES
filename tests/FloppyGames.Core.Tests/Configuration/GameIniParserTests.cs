using FloppyGames.Core.Configuration;

namespace FloppyGames.Core.Tests.Configuration;

public class GameIniParserTests
{
    [Fact]
    public void Parse_FullValidIni_ReturnsConfigWithAllFields()
    {
        const string ini = """
            [Game]
            TITLE=Half-Life 2
            APPID=220
            PROCESS=hl2.exe
            COVER=cover.jpg

            [Options]
            WatchTimeoutSeconds=45
            LaunchDelaySeconds=5
            GracefulShutdown=false
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Equal("Half-Life 2", result.Config!.Title);
        Assert.Equal(220, result.Config.AppId);
        Assert.Equal("hl2.exe", result.Config.Process);
        Assert.Equal("cover.jpg", result.Config.Cover);
        Assert.Equal(45, result.Config.WatchTimeoutSeconds);
        Assert.Equal(5, result.Config.LaunchDelaySeconds);
        Assert.False(result.Config.GracefulShutdown);
    }

    [Fact]
    public void Parse_WithDescription_ReturnsIt()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe
            DESCRIPTION=Um puzzle em primeira pessoa com um portal gun.
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Equal("Um puzzle em primeira pessoa com um portal gun.", result.Config!.Description);
    }

    [Fact]
    public void Parse_WithoutDescription_IsNull()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Null(result.Config!.Description);
    }

    [Fact]
    public void Parse_LaunchDelayZero_IsValid_MeansLaunchImmediately()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe

            [Options]
            LaunchDelaySeconds=0
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Equal(0, result.Config!.LaunchDelaySeconds);
    }

    [Fact]
    public void Parse_WatchTimeoutZero_IsInvalid_SinceItWouldNeverConfirmLaunch()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe

            [Options]
            WatchTimeoutSeconds=0
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("WatchTimeoutSeconds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_MinimalValidIni_AppliesDefaults()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Null(result.Config!.Cover);
        Assert.Equal(30, result.Config.WatchTimeoutSeconds);
        Assert.Equal(2, result.Config.LaunchDelaySeconds);
        Assert.True(result.Config.GracefulShutdown);
    }

    [Fact]
    public void Parse_IgnoresCommentsBlankLinesAndIsCaseInsensitive()
    {
        const string ini = """
            ; ficheiro de configuração de exemplo
            [game]
            title = Portal 2
            # outro estilo de comentário
            appid = 620

            process = portal2.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Equal("Portal 2", result.Config!.Title);
        Assert.Equal(620, result.Config.AppId);
        Assert.Equal("portal2.exe", result.Config.Process);
    }

    [Theory]
    [InlineData("TITLE")]
    [InlineData("APPID")]
    [InlineData("PROCESS")]
    public void Parse_MissingRequiredField_Fails(string missingField)
    {
        var fields = new Dictionary<string, string>
        {
            ["TITLE"] = "Portal",
            ["APPID"] = "400",
            ["PROCESS"] = "portal.exe",
        };
        fields.Remove(missingField);

        var ini = "[Game]\n" + string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Null(result.Config);
        Assert.Contains(result.Errors, e => e.Contains(missingField, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Parse_InvalidAppId_Fails(string appId)
    {
        var ini = $"""
            [Game]
            TITLE=Portal
            APPID={appId}
            PROCESS=portal.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("APPID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_InvalidOptionalNumericField_FailsInsteadOfSilentlyFallingBack()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe

            [Options]
            WatchTimeoutSeconds=not-a-number
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("WatchTimeoutSeconds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_InvalidGracefulShutdownValue_Fails()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe

            [Options]
            GracefulShutdown=maybe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("GracefulShutdown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_AccumulatesAllErrorsAtOnce()
    {
        const string ini = """
            [Game]
            APPID=abc
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Parse_EmptyIni_FailsWithAllRequiredFieldErrors()
    {
        var result = GameIniParser.Parse(string.Empty);

        Assert.False(result.Success);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Parse_WithoutPlatform_DefaultsToSteam()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            APPID=400
            PROCESS=portal.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Equal(GamePlatform.Steam, result.Config!.Platform);
        Assert.Equal(400, result.Config.AppId);
    }

    [Theory]
    [InlineData("epic", GamePlatform.Epic)]
    [InlineData("EPIC", GamePlatform.Epic)]
    [InlineData("gog", GamePlatform.Gog)]
    [InlineData("steam", GamePlatform.Steam)]
    public void Parse_PlatformIsCaseInsensitive(string raw, GamePlatform expected)
    {
        var ini = expected switch
        {
            GamePlatform.Epic => $"""
                [Game]
                TITLE=Inside
                PLATFORM={raw}
                EPIC_NAMESPACE=ns
                EPIC_ITEM=item
                EPIC_APP=app
                PROCESS=INSIDE.exe
                """,
            GamePlatform.Gog => $"""
                [Game]
                TITLE=Some Game
                PLATFORM={raw}
                GOG_ID=1234567890
                PROCESS=game.exe
                """,
            _ => $"""
                [Game]
                TITLE=Portal
                PLATFORM={raw}
                APPID=400
                PROCESS=portal.exe
                """,
        };

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Config!.Platform);
    }

    [Fact]
    public void Parse_UnknownPlatform_Fails()
    {
        const string ini = """
            [Game]
            TITLE=Portal
            PLATFORM=EPICGAMESSTOREV2
            PROCESS=portal.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("PLATFORM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_EpicPlatform_DoesNotRequireAppId()
    {
        const string ini = """
            [Game]
            TITLE=Inside
            PLATFORM=EPIC
            EPIC_NAMESPACE=ns
            EPIC_ITEM=item
            EPIC_APP=app
            PROCESS=INSIDE.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Null(result.Config!.AppId);
        Assert.Equal("ns", result.Config.EpicNamespace);
        Assert.Equal("item", result.Config.EpicItemId);
        Assert.Equal("app", result.Config.EpicAppName);
    }

    [Theory]
    [InlineData("EPIC_NAMESPACE")]
    [InlineData("EPIC_ITEM")]
    [InlineData("EPIC_APP")]
    public void Parse_EpicPlatform_MissingIdentityField_Fails(string missingField)
    {
        var fields = new Dictionary<string, string>
        {
            ["EPIC_NAMESPACE"] = "ns",
            ["EPIC_ITEM"] = "item",
            ["EPIC_APP"] = "app",
        };
        fields.Remove(missingField);

        var ini = "[Game]\nTITLE=Inside\nPLATFORM=EPIC\nPROCESS=INSIDE.exe\n" +
                   string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains(missingField, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_GogPlatform_RequiresGogId()
    {
        const string ini = """
            [Game]
            TITLE=Some Game
            PLATFORM=GOG
            PROCESS=game.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("GOG_ID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_GogPlatform_DoesNotRequireAppId()
    {
        const string ini = """
            [Game]
            TITLE=Some Game
            PLATFORM=GOG
            GOG_ID=1234567890
            PROCESS=game.exe
            """;

        var result = GameIniParser.Parse(ini);

        Assert.True(result.Success);
        Assert.Null(result.Config!.AppId);
        Assert.Equal("1234567890", result.Config.GogGameId);
    }
}
