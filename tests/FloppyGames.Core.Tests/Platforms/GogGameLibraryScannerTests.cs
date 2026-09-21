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
            """;
        command.ExecuteNonQuery();
    }

    private void InsertGame(
        long productId, string name, string installPath,
        long? diskSize = null, string? executablePath = null, bool isPrimary = true)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        Execute(connection, "INSERT INTO Products (id, name) VALUES ($id, $name)",
            ("$id", productId), ("$name", name));

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
}
