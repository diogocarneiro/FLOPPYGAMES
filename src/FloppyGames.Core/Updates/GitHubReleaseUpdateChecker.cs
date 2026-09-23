using System.Text.Json;

namespace FloppyGames.Core.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,

    /// <summary>Sem ligação, GitHub indisponível, repositório privado ou ainda sem releases publicadas.</summary>
    Failed,
}

/// <summary>Uma versão publicada no GitHub, mais recente do que a instalada.</summary>
public sealed record AvailableUpdate(Version Version, string ReleasePageUrl, string? InstallerUrl);

public sealed record UpdateCheckResult(UpdateCheckStatus Status, AvailableUpdate? Update = null);

/// <summary>
/// Procura atualizações na página de releases do GitHub do projeto — as mesmas releases que o
/// pipeline (<c>.github/workflows/release.yml</c>) publica a cada push, com o instalador
/// <c>FloppyGamesSetup.exe</c> anexado. Usa a API pública sem autenticação: só funciona com o
/// repositório público (um repositório privado responde 404, tratado como <see cref="UpdateCheckStatus.Failed"/>).
/// </summary>
public sealed class GitHubReleaseUpdateChecker
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/diogocarneiro/FLOPPYGAMES/releases/latest";

    private const string InstallerAssetName = "FloppyGamesSetup.exe";

    private static readonly HttpMessageHandler SharedHandler = new SocketsHttpHandler();

    private readonly HttpClient _apiClient;
    private readonly HttpClient _downloadClient;

    public GitHubReleaseUpdateChecker(HttpMessageHandler? handler = null)
    {
        handler ??= SharedHandler;

        _apiClient = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
        // O instalador tem ~100 MB: o limite de tempo global do HttpClient não pode cortar o
        // download a meio — quem chama controla-o pelo CancellationToken.
        _downloadClient = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };

        foreach (var client in new[] { _apiClient, _downloadClient })
        {
            // A API do GitHub recusa pedidos sem User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FloppyGames-UpdateChecker");
        }

        _apiClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _apiClient.GetAsync(LatestReleaseApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed);
            }

            var latest = ParseLatestRelease(await response.Content.ReadAsStringAsync(cancellationToken));
            if (latest is null)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed);
            }

            return IsNewer(latest.Version, currentVersion)
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, latest)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
    }

    /// <summary>Descarrega o instalador para a pasta temporária e devolve o caminho. Lança em caso de falha.</summary>
    public async Task<string> DownloadInstallerAsync(AvailableUpdate update, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (update.InstallerUrl is null)
        {
            throw new InvalidOperationException($"A release {update.Version} não tem o instalador {InstallerAssetName} anexado.");
        }

        var path = Path.Combine(Path.GetTempPath(), $"FloppyGamesSetup-{update.Version}.exe");

        using var response = await _downloadClient.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long bytesRead = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;
                if (totalBytes > 0)
                {
                    progress?.Report((double)bytesRead / totalBytes.Value);
                }
            }
        }

        return path;
    }

    /// <summary>Lê a resposta de <c>/releases/latest</c>: versão (da tag <c>vX.Y.Z</c>), página da release e URL do instalador.</summary>
    public static AvailableUpdate? ParseLatestRelease(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!TryParseVersion(GetString(root, "tag_name"), out var version) || GetString(root, "html_url") is not { } pageUrl)
            {
                return null;
            }

            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                installerUrl = assets.EnumerateArray()
                    .Where(asset => string.Equals(GetString(asset, "name"), InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                    .Select(asset => GetString(asset, "browser_download_url"))
                    .FirstOrDefault(url => url is not null);
            }

            return new AvailableUpdate(version, pageUrl, installerUrl);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsNewer(Version latest, string currentVersion) =>
        !TryParseVersion(currentVersion, out var current) || Normalize(latest) > Normalize(current);

    /// <summary>Aceita "0.1.7" e "v0.1.7" (tag), ignorando um eventual sufixo "+commit".</summary>
    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim().TrimStart('v', 'V').Split('+', '-')[0];
        if (!Version.TryParse(core, out var parsed))
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }

    // "0.1" e "0.1.0" têm de comparar como iguais — Version trata componentes em falta como -1.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
