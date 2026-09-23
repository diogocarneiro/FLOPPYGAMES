using FloppyGames.Core.Configuration;
using FloppyGames.Core.Platforms;
using Microsoft.Data.Sqlite;

namespace FloppyGames.Core.Tests.Platforms;

/// <summary>
/// Testa contra o esquema real do <c>galaxy-2.0.db</c> do GOG Galaxy 2.0 (confirmado com
/// <c>sqlite3</c> contra a base de dados de uma instalação real) — só as tabelas/colunas
/// efetivamente usadas pelo scanner, recriadas aqui numa base de dados temporária.
/// </summary>
public class GogGameLibraryScannerTests : IDisposable
{
    private readonly string _dbPath;

    public GogGameLibraryScannerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "FloppyGamesTests_" + Guid.NewGuid() + ".db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private void CreateSchema()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Products(id INTEGER NOT NULL PRIMARY KEY, name TEXT NULL, parentId INTEGER NULL);
            CREATE TABLE ReleaseKeys(key TEXT NOT NULL PRIMARY KEY);
            CREATE TABLE InstalledBaseProducts(productId INTEGER NOT NULL, generation INTEGER NOT NULL, languageId INTEGER NOT NULL, installationPath TEXT NOT NULL, installationId INT64 NOT NULL, buildId INT64 NULL, branch TEXT NULL, installationDate TEXT NULL);
            CREATE TABLE DiskSizes(gameReleaseKey TEXT NOT NULL, diskSize INT64, diskDrive TEXT);
            CREATE TABLE PlayTaskTypes(id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, type TEXT NOT NULL);
            CREATE TABLE PlayTasks(id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, gameReleaseKey TEXT NOT NULL, userId INT64 NULL, "order" INTEGER NOT NULL, typeId INTEGER NOT NULL, isPrimary BOOLEAN NOT NULL);
            CREATE TABLE PlayTaskLaunchParameters(playTaskId INTEGER NOT NULL, executablePath TEXT NULL, commandLineArgs TEXT NULL, label TEXT NULL);
            CREATE TABLE GamePieceTypes(id INTEGER PRIMARY KEY AUTOINCREMENT, type TEXT UNIQUE NOT NULL);
            CREATE TABLE GamePieces(releaseKey TEXT NOT NULL, gamePieceTypeId INTEGER NOT NULL, userId INTEGER NOT NULL, value TEXT NOT NULL, languageId INTEGER);
            CREATE TABLE LimitedDetails(id INTEGER PRIMARY KEY AUTOINCREMENT, productId INTEGER NOT NULL, languageId INTEGER NOT NULL, is_production INTEGER, stored_at TEXT, title TEXT, links TEXT, images TEXT);
            INSERT INTO GamePieceTypes (id, type) VALUES (4, 'originalImages'), (21, 'title');
            """;
        command.ExecuteNonQuery();
    }

    private void InsertGame(
        long productId, string? name, string installPath,
        long? diskSize = null, string? executablePath = null, bool isPrimary = true)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        Execute(connection, "INSERT INTO Products (id, name) VALUES ($id, $name)",
            ("$id", productId), ("$name", (object?)name ?? DBNull.Value));

        var releaseKey = $"gog_{productId}";
        Execute(connection, "INSERT INTO ReleaseKeys (key) VALUES ($key)", ("$key", releaseKey));

        Execute(connection, """
            INSERT INTO InstalledBaseProducts (productId, generation, languageId, installationPath, installationId, buildId, branch, installationDate)
            VALUES ($productId, 2, 1, $installPath, $productId, NULL, NULL, '2026-01-01T00:00:00Z')
            """,
            ("$productId", productId), ("$installPath", installPath));

        if (diskSize is not null)
        {
            Execute(connection, "INSERT INTO DiskSizes (gameReleaseKey, diskSize, diskDrive) VALUES ($key, $size, 'C:')",
                ("$key", releaseKey), ("$size", diskSize.Value));
        }

        if (executablePath is not null)
        {
            Execute(connection, """INSERT INTO PlayTasks (gameReleaseKey, userId, "order", typeId, isPrimary) VALUES ($key, NULL, 0, 1, $primary)""",
                ("$key", releaseKey), ("$primary", isPrimary ? 1L : 0L));

            using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid()";
            var playTaskId = (long)idCommand.ExecuteScalar()!;

            Execute(connection, "INSERT INTO PlayTaskLaunchParameters (playTaskId, executablePath) VALUES ($id, $exe)",
                ("$id", playTaskId), ("$exe", executablePath));
        }
    }

    private void InsertGamePiece(long productId, int typeId, string jsonValue)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        Execute(connection, "INSERT INTO GamePieces (releaseKey, gamePieceTypeId, userId, value, languageId) VALUES ($key, $type, 1, $value, NULL)",
            ("$key", $"gog_{productId}"), ("$type", typeId), ("$value", jsonValue));
    }

    private void InsertLimitedDetailsTitle(long productId, string title)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        Execute(connection, "INSERT INTO LimitedDetails (productId, languageId, title) VALUES ($id, 1, $title)",
            ("$id", productId), ("$title", title));
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    [Fact]
    public void ScanInstalledGames_DatabaseDoesNotExist_ReturnsEmpty()
    {
        var scanner = new GogGameLibraryScanner(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".db"));

        Assert.Empty(scanner.ScanInstalledGames());
    }

    [Fact]
    public void ScanInstalledGames_OneInstalledGame_ReturnsItWithSize()
    {
        CreateSchema();
        InsertGame(1207658930, "The Witcher 3: Wild Hunt", @"C:\GOG Games\The Witcher 3 Wild Hunt", diskSize: 53687091200);
        var scanner = new GogGameLibraryScanner(_dbPath);

        var game = Assert.Single(scanner.ScanInstalledGames());

        Assert.Equal("The Witcher 3: Wild Hunt", game.Name);
        Assert.Equal(GamePlatform.Gog, game.Platform);
        Assert.Equal(@"C:\GOG Games\The Witcher 3 Wild Hunt", game.InstallPath);
        Assert.Equal(53687091200, game.InstalledSizeBytes);
        Assert.Equal("1207658930", game.GogGameId);
    }

    [Fact]
    public void ScanInstalledGames_NoDiskSizeRow_LeavesSizeNull()
    {
        CreateSchema();
        InsertGame(42, "Some Game", @"C:\GOG Games\Some Game");
        var scanner = new GogGameLibraryScanner(_dbPath);

        var game = Assert.Single(scanner.ScanInstalledGames());

        Assert.Null(game.InstalledSizeBytes);
    }

    [Fact]
    public void ScanInstalledGames_ResultsSortedAlphabeticallyByName()
    {
        CreateSchema();
        InsertGame(2, "Zebra Quest", @"C:\Games\Zebra");
        InsertGame(1, "Alpha Adventure", @"C:\Games\Alpha");
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Equal(["Alpha Adventure", "Zebra Quest"], scanner.ScanInstalledGames().Select(g => g.Name));
    }

    [Fact]
    public void TryResolveExePath_PrimaryPlayTaskExists_CombinesInstallPathAndRelativeExe()
    {
        CreateSchema();
        InsertGame(1207658930, "The Witcher 3: Wild Hunt", @"C:\GOG Games\The Witcher 3 Wild Hunt", executablePath: @"bin\x64\witcher3.exe");
        var scanner = new GogGameLibraryScanner(_dbPath);

        var exePath = scanner.TryResolveExePath("1207658930");

        Assert.Equal(@"C:\GOG Games\The Witcher 3 Wild Hunt\bin\x64\witcher3.exe", exePath);
    }

    [Fact]
    public void TryResolveExePath_UnknownGameId_ReturnsNull()
    {
        CreateSchema();
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Null(scanner.TryResolveExePath("999"));
    }

    [Fact]
    public void TryResolveExePath_NonPrimaryPlayTaskOnly_ReturnsNull()
    {
        CreateSchema();
        InsertGame(1, "Some Game", @"C:\Games\Some Game", executablePath: "extras.exe", isPrimary: false);
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Null(scanner.TryResolveExePath("1"));
    }

    [Fact]
    public void TryResolveExePath_NotANumber_ReturnsNull()
    {
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Null(scanner.TryResolveExePath("not-a-number"));
    }

    /// <summary>
    /// Regressão: numa instalação real do Galaxy, os dois jogos instalados (demos) tinham
    /// <c>Products.name</c> a NULL — o título que o Galaxy mostra está em GamePieces.
    /// </summary>
    [Fact]
    public void ScanInstalledGames_ProductWithoutName_UsesGalaxyTitlePiece()
    {
        CreateSchema();
        InsertGame(2013434102, null, @"C:\Program Files\GOG Galaxy\Games\IRON NEST Heavy Turret Simulator Demo");
        InsertGamePiece(2013434102, 21, """{"title":"IRON NEST: Heavy Turret Simulator Demo"}""");
        var scanner = new GogGameLibraryScanner(_dbPath);

        var game = Assert.Single(scanner.ScanInstalledGames());

        Assert.Equal("IRON NEST: Heavy Turret Simulator Demo", game.Name);
        Assert.Equal("2013434102", game.GogGameId);
    }

    [Fact]
    public void ScanInstalledGames_NoNameAndNoTitlePiece_FallsBackToLimitedDetails()
    {
        CreateSchema();
        InsertGame(1337535322, null, @"C:\Games\Alien Breed Demo");
        InsertLimitedDetailsTitle(1337535322, "Alien Breed 35th Anniversary Demo");
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Equal("Alien Breed 35th Anniversary Demo", Assert.Single(scanner.ScanInstalledGames()).Name);
    }

    [Fact]
    public void TryResolveCoverUrl_VerticalCoverInWebp_ReturnsJpgUrl()
    {
        CreateSchema();
        InsertGame(2013434102, null, @"C:\Games\Iron Nest");
        InsertGamePiece(2013434102, 4,
            """{"background":"https://images.gog.com/bg.webp?namespace=gamesdb","verticalCover":"https://images.gog.com/f599a0_glx_vertical_cover.webp?namespace=gamesdb"}""");
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Equal("https://images.gog.com/f599a0_glx_vertical_cover.jpg?namespace=gamesdb", scanner.TryResolveCoverUrl("2013434102"));
    }

    [Fact]
    public void TryResolveCoverUrl_NoImagesPiece_ReturnsNull()
    {
        CreateSchema();
        InsertGame(42, "Some Game", @"C:\Games\Some Game");
        var scanner = new GogGameLibraryScanner(_dbPath);

        Assert.Null(scanner.TryResolveCoverUrl("42"));
    }
}
