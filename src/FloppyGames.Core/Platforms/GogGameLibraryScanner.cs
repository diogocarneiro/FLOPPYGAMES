using System.Text.RegularExpressions;
using FloppyGames.Core.Configuration;
using Microsoft.Data.Sqlite;

namespace FloppyGames.Core.Platforms;

/// <summary>
/// Lê os jogos GOG instalados a partir da própria base de dados do GOG Galaxy 2.0
/// (<c>%ProgramData%\GOG.com\Galaxy\storage\galaxy-2.0.db</c>, SQLite).
/// <para>
/// Este esquema foi <b>confirmado diretamente</b> contra a base de dados de uma instalação real e
/// em execução do GOG Galaxy — <c>Products</c> (id, name), <c>InstalledBaseProducts</c>
/// (productId → installationPath), <c>DiskSizes</c> e <c>PlayTasks</c>/<c>PlayTaskLaunchParameters</c>
/// (a tarefa de arranque principal, <c>isPrimary = 1</c>, dá o executável correto a lançar) são
/// tabelas reais desse ficheiro, não suposição. Uma tentativa anterior desta classe assumia que a
/// GOG escrevia entradas por jogo no Registo (como as instalações standalone antigas faziam) — o
/// GOG Galaxy 2.0 não o faz de todo; essa base foi a lição de que "documentado pela comunidade"
/// não substitui inspecionar os dados reais.
/// </para>
/// <para>
/// A base de dados está aberta em modo só-leitura (o próprio cliente Galaxy pode tê-la aberta em
/// simultâneo) e qualquer falha — ficheiro em falta, bloqueado, ou colunas inesperadas — resulta
/// em lista vazia / <c>null</c>, nunca numa exceção não tratada.
/// </para>
/// </summary>
public sealed partial class GogGameLibraryScanner
{
    private static readonly string DefaultDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GOG.com", "Galaxy", "storage", "galaxy-2.0.db");

    private readonly string _databasePath;

    public GogGameLibraryScanner(string? databasePath = null)
    {
        _databasePath = databasePath ?? DefaultDatabasePath;
    }

    public IReadOnlyList<DiscoveredGame> ScanInstalledGames()
    {
        if (!File.Exists(_databasePath))
        {
            return [];
        }

        // Products.name vem a NULL para jogos instalados fora da biblioteca comprada (ex. demos
        // instaladas pelo Galaxy) — verificado numa base de dados real com dois jogos instalados e
        // os dois assim, o que fazia a lista sair vazia. O título que o próprio Galaxy mostra está
        // em GamePieces ('title', JSON); LimitedDetails.title é o último recurso.
        const string sql = """
            SELECT ibp.productId,
                   COALESCE(
                       NULLIF(p.name, ''),
                       (SELECT json_extract(gp.value, '$.title')
                        FROM GamePieces gp JOIN GamePieceTypes gpt ON gpt.id = gp.gamePieceTypeId
                        WHERE gp.releaseKey = 'gog_' || ibp.productId AND gpt.type = 'title'
                        LIMIT 1),
                       (SELECT ld.title FROM LimitedDetails ld
                        WHERE ld.productId = ibp.productId AND ld.title IS NOT NULL
                        LIMIT 1)) AS name,
                   ibp.installationPath, ds.diskSize
            FROM InstalledBaseProducts ibp
            LEFT JOIN Products p ON p.id = ibp.productId
            LEFT JOIN DiskSizes ds ON ds.gameReleaseKey = 'gog_' || ibp.productId
            ORDER BY name COLLATE NOCASE
            """;

        try
        {
            using var connection = OpenReadOnly();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();

            var games = new List<DiscoveredGame>();
            while (reader.Read())
            {
                var productId = reader.GetInt64(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                var installPath = reader.IsDBNull(2) ? null : reader.GetString(2);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installPath))
                {
                    continue;
                }

                var diskSize = reader.IsDBNull(3) ? (long?)null : reader.GetInt64(3);
                games.Add(new DiscoveredGame(
                    name, GamePlatform.Gog, installPath, diskSize, GogGameId: productId.ToString()));
            }

            return games;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Resolve o executável instalado de um jogo pelo seu GOG_ID, a partir da tarefa de arranque
    /// principal (<c>PlayTasks.isPrimary = 1</c>) — usado no momento do lançamento.
    /// </summary>
    public string? TryResolveExePath(string gogGameId)
    {
        if (!long.TryParse(gogGameId, out var productId) || !File.Exists(_databasePath))
        {
            return null;
        }

        const string sql = """
            SELECT ibp.installationPath, ptlp.executablePath
            FROM InstalledBaseProducts ibp
            JOIN PlayTasks pt ON pt.gameReleaseKey = 'gog_' || ibp.productId
            JOIN PlayTaskLaunchParameters ptlp ON ptlp.playTaskId = pt.id
            WHERE ibp.productId = $productId AND pt.isPrimary = 1
            """;

        try
        {
            using var connection = OpenReadOnly();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$productId", productId);
            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            var installPath = reader.IsDBNull(0) ? null : reader.GetString(0);
            var executablePath = reader.IsDBNull(1) ? null : reader.GetString(1);

            if (string.IsNullOrWhiteSpace(installPath) || string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            return Path.IsPathRooted(executablePath) ? executablePath : Path.Combine(installPath, executablePath);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// O URL da capa vertical do jogo, tal como o GOG Galaxy a guarda
    /// (<c>GamePieces</c> do tipo <c>originalImages</c>, campo <c>verticalCover</c>). O Galaxy
    /// guarda-a em WebP, que o WPF só descodifica se a extensão WebP do Windows estiver instalada
    /// — o mesmo endereço com <c>.jpg</c> devolve um JPEG real (verificado ao vivo), por isso é
    /// esse que se pede.
    /// </summary>
    public string? TryResolveCoverUrl(string gogGameId)
    {
        if (!long.TryParse(gogGameId, out var productId) || !File.Exists(_databasePath))
        {
            return null;
        }

        const string sql = """
            SELECT json_extract(gp.value, '$.verticalCover')
            FROM GamePieces gp JOIN GamePieceTypes gpt ON gpt.id = gp.gamePieceTypeId
            WHERE gp.releaseKey = $releaseKey AND gpt.type = 'originalImages'
            LIMIT 1
            """;

        try
        {
            using var connection = OpenReadOnly();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$releaseKey", $"gog_{productId}");

            return command.ExecuteScalar() is string url && !string.IsNullOrWhiteSpace(url)
                ? WebpExtension().Replace(url, ".jpg")
                : null;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\.webp(?=\?|$)", RegexOptions.IgnoreCase)]
    private static partial Regex WebpExtension();

    private SqliteConnection OpenReadOnly()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly");
        connection.Open();
        return connection;
    }
}
