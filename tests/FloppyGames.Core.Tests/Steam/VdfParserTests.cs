using FloppyGames.Core.Steam;

namespace FloppyGames.Core.Tests.Steam;

public class VdfParserTests
{
    [Fact]
    public void Parse_SimpleKeyValuePairs_ReadsAllValues()
    {
        const string vdf = """
            "AppState"
            {
                "appid"        "730"
                "name"        "Counter-Strike 2"
                "installdir"        "Counter-Strike Global Offensive"
            }
            """;

        var root = VdfParser.Parse(vdf);

        Assert.Equal("730", root.GetString("appid"));
        Assert.Equal("Counter-Strike 2", root.GetString("name"));
        Assert.Equal("Counter-Strike Global Offensive", root.GetString("installdir"));
    }

    [Fact]
    public void Parse_NestedSections_ReadsChildValues()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"        "C:\\Games\\SteamLibrary"
                    "label"        ""
                }
                "1"
                {
                    "path"        "D:\\SteamLibrary2"
                }
            }
            """;

        var root = VdfParser.Parse(vdf);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal(@"C:\Games\SteamLibrary", root["0"]!.GetString("path"));
        Assert.Equal(@"D:\SteamLibrary2", root["1"]!.GetString("path"));
    }

    [Fact]
    public void Parse_IgnoresLineComments()
    {
        const string vdf = """
            "AppState"
            {
                // isto é um comentário
                "appid"        "400" // outro comentário
                "name"        "Portal"
            }
            """;

        var root = VdfParser.Parse(vdf);

        Assert.Equal("400", root.GetString("appid"));
        Assert.Equal("Portal", root.GetString("name"));
    }

    [Fact]
    public void Parse_MissingClosingBrace_ThrowsFormatException()
    {
        const string vdf = """
            "AppState"
            {
                "appid"        "400"
            """;

        Assert.Throws<FormatException>(() => VdfParser.Parse(vdf));
    }

    [Fact]
    public void Parse_UnterminatedString_ThrowsFormatException()
    {
        const string vdf = """
            "AppState"
            {
                "appid"        "400
            }
            """;

        Assert.Throws<FormatException>(() => VdfParser.Parse(vdf));
    }
}
