using System.Globalization;
using FloppyGames.Core.Localization;

namespace FloppyGames.Core.Tests.Localization;

public class StringsTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
        CultureInfo.CurrentCulture = _originalCulture;
    }

    [Theory]
    [InlineData("en-US", "Preparing...")]
    [InlineData("pt-PT", "A preparar...")]
    [InlineData("es-ES", "Preparando...")]
    [InlineData("it-IT", "Preparazione...")]
    public void Splash_Preparing_ResolvesPerSatelliteCulture(string cultureName, string expected)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal(expected, Strings.Splash_Preparing);
    }

    [Fact]
    public void UnsupportedCulture_FallsBackToFrenchNeutralResource()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("Préparation...", Strings.Splash_Preparing);
    }

    [Fact]
    public void FormattedString_SubstitutesArguments()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("3 games found.", Strings.LS_GamesFoundMany(3));
        Assert.Equal("INSIDE is running.", Strings.Splash_GameRunning("INSIDE"));
    }
}

public class LocalizationManagerTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
        CultureInfo.CurrentCulture = _originalCulture;
    }

    [Fact]
    public void Apply_KnownLanguageCode_SetsCultureAndUiCulture()
    {
        LocalizationManager.Apply("es");

        Assert.Equal("es-ES", CultureInfo.CurrentCulture.Name);
        Assert.Equal("es-ES", CultureInfo.CurrentUICulture.Name);
        Assert.Equal("es-ES", CultureInfo.DefaultThreadCurrentCulture?.Name);
        Assert.Equal("es-ES", CultureInfo.DefaultThreadCurrentUICulture?.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    public void Apply_MissingOrUnknownLanguageCode_DefaultsToFrench(string? languageCode)
    {
        LocalizationManager.Apply(languageCode);

        Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void SupportedLanguages_HasExactlyTheFiveExpectedCodes()
    {
        Assert.Equal(["fr", "en", "pt", "es", "it"], SupportedLanguages.All.Select(l => l.Code));
    }
}
