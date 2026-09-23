using System.Net;
using FloppyGames.Core.Updates;

namespace FloppyGames.Core.Tests.Updates;

public class GitHubReleaseUpdateCheckerTests
{
    private const string ReleaseWithInstaller = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/diogocarneiro/FLOPPYGAMES/releases/tag/v0.2.0",
          "assets": [
            { "name": "FloppyGamesSetup.exe", "browser_download_url": "https://github.com/diogocarneiro/FLOPPYGAMES/releases/download/v0.2.0/FloppyGamesSetup.exe" }
          ]
        }
        """;

    [Theory]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.1.1", false)]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.2.0", "0.1.9", true)]
    [InlineData("1.0.0", "0.9.9", true)]
    public void IsNewer_ComparesSemanticVersionsCorrectly(string latest, string current, bool expected)
    {
        Assert.True(GitHubReleaseUpdateChecker.TryParseVersion(latest, out var latestVersion));

        Assert.Equal(expected, GitHubReleaseUpdateChecker.IsNewer(latestVersion, current));
    }

    [Fact]
    public void IsNewer_MissingBuildComponent_ComparesEqualToZeroBuild()
    {
        Assert.True(GitHubReleaseUpdateChecker.TryParseVersion("0.1", out var latestVersion));

        Assert.False(GitHubReleaseUpdateChecker.IsNewer(latestVersion, "0.1.0"));
    }

    [Fact]
    public void IsNewer_CurrentVersionUnparsable_TreatsLatestAsNewer()
    {
        Assert.True(GitHubReleaseUpdateChecker.TryParseVersion("0.1.0", out var latestVersion));

        Assert.True(GitHubReleaseUpdateChecker.IsNewer(latestVersion, "not-a-version"));
    }

    [Theory]
    [InlineData("0.1.7", true)]
    [InlineData("v0.1.7", true)]
    [InlineData("V0.1.7", true)]
    [InlineData("0.1.7+abcdef1", true)]
    [InlineData("0.1.7-beta", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not-a-version", false)]
    public void TryParseVersion_HandlesTagPrefixAndSuffixes(string? text, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, GitHubReleaseUpdateChecker.TryParseVersion(text, out _));
    }

    [Fact]
    public void ParseLatestRelease_ValidJsonWithInstallerAsset_ReturnsAllFields()
    {
        var update = GitHubReleaseUpdateChecker.ParseLatestRelease(ReleaseWithInstaller);

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0), update!.Version);
        Assert.Equal("https://github.com/diogocarneiro/FLOPPYGAMES/releases/tag/v0.2.0", update.ReleasePageUrl);
        Assert.Equal(
            "https://github.com/diogocarneiro/FLOPPYGAMES/releases/download/v0.2.0/FloppyGamesSetup.exe",
            update.InstallerUrl);
    }

    [Fact]
    public void ParseLatestRelease_NoMatchingAsset_LeavesInstallerUrlNull()
    {
        const string json = """
            { "tag_name": "v0.2.0", "html_url": "https://example.com", "assets": [ { "name": "other.zip", "browser_download_url": "https://example.com/other.zip" } ] }
            """;

        var update = GitHubReleaseUpdateChecker.ParseLatestRelease(json);

        Assert.NotNull(update);
        Assert.Null(update!.InstallerUrl);
    }

    [Fact]
    public void ParseLatestRelease_MissingTagName_ReturnsNull()
    {
        const string json = """{ "html_url": "https://example.com" }""";

        Assert.Null(GitHubReleaseUpdateChecker.ParseLatestRelease(json));
    }

    [Fact]
    public void ParseLatestRelease_MalformedJson_ReturnsNull()
    {
        Assert.Null(GitHubReleaseUpdateChecker.ParseLatestRelease("not json"));
    }

    [Fact]
    public async Task CheckAsync_NewerReleaseAvailable_ReturnsUpdateAvailable()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ReleaseWithInstaller);
        var checker = new GitHubReleaseUpdateChecker(handler);

        var result = await checker.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(0, 2, 0), result.Update!.Version);
    }

    [Fact]
    public async Task CheckAsync_AlreadyOnLatestVersion_ReturnsUpToDate()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ReleaseWithInstaller);
        var checker = new GitHubReleaseUpdateChecker(handler);

        var result = await checker.CheckAsync("0.2.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task CheckAsync_RepositoryPrivateOrNoReleasesYet_ReturnsFailed()
    {
        // Uma releases/latest de um repo privado (ou sem releases publicadas) devolve 404 — o
        // motivo original de precisar de tornar o repositório público para isto funcionar.
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, "{}");
        var checker = new GitHubReleaseUpdateChecker(handler);

        var result = await checker.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task CheckAsync_NetworkFailure_ReturnsFailedWithoutThrowing()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("no network"));
        var checker = new GitHubReleaseUpdateChecker(handler);

        var result = await checker.CheckAsync("0.1.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task CheckAsync_SendsUserAgentHeader_AsGitHubApiRequires()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ReleaseWithInstaller);
        var checker = new GitHubReleaseUpdateChecker(handler);

        await checker.CheckAsync("0.1.0", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.NotEmpty(handler.LastRequest!.Headers.UserAgent);
    }

    [Fact]
    public async Task DownloadInstallerAsync_NoInstallerAssetOnRelease_Throws()
    {
        var checker = new GitHubReleaseUpdateChecker(new FakeHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var update = new AvailableUpdate(new Version(0, 2, 0), "https://example.com", InstallerUrl: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => checker.DownloadInstallerAsync(update, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadInstallerAsync_WritesBytesToTempFileAndReportsProgress()
    {
        var payload = new byte[250_000];
        Random.Shared.NextBytes(payload);
        var handler = new FakeHttpMessageHandler(payload);
        var checker = new GitHubReleaseUpdateChecker(handler);
        var update = new AvailableUpdate(new Version(0, 2, 0), "https://example.com", "https://example.com/FloppyGamesSetup.exe");
        var reports = new List<double>();

        var path = await checker.DownloadInstallerAsync(update, new SynchronousProgress<double>(reports.Add), CancellationToken.None);

        try
        {
            Assert.Equal(payload, await File.ReadAllBytesAsync(path));
            Assert.NotEmpty(reports);
            Assert.Equal(1.0, reports[^1]);
            Assert.All(reports, r => Assert.InRange(r, 0.0, 1.0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    /// <summary>
    /// Substitui o transporte HTTP real por uma resposta fixa (ou uma exceção), para os testes
    /// nunca dependerem de rede real nem do GitHub estar no ar.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly string? _stringContent;
        private readonly byte[]? _byteContent;
        private readonly Exception? _exceptionToThrow;

        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _stringContent = content;
        }

        public FakeHttpMessageHandler(byte[] content)
        {
            _statusCode = HttpStatusCode.OK;
            _byteContent = content;
        }

        public FakeHttpMessageHandler(Exception exceptionToThrow) => _exceptionToThrow = exceptionToThrow;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (_exceptionToThrow is not null)
            {
                throw _exceptionToThrow;
            }

            var response = new HttpResponseMessage(_statusCode!.Value)
            {
                Content = _byteContent is not null ? new ByteArrayContent(_byteContent) : new StringContent(_stringContent!),
            };

            return Task.FromResult(response);
        }
    }
}
