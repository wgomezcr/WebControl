using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace WebControl.Agent.Windows.Persistence;

public sealed class WebControlDatabase
{
    private readonly WebControlDbOptions _options;
    private readonly ILogger<WebControlDatabase> _logger;

    public WebControlDatabase(
        IOptions<WebControlDbOptions> options,
        ILogger<WebControlDatabase> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string DatabasePath =>
        Environment.ExpandEnvironmentVariables(
            _options.Path);

    public string ConnectionString =>
        new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new InvalidOperationException(
                "SQLite database path is not configured.");
        }

        var directory =
            System.IO.Path.GetDirectoryName(
                DatabasePath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "SQLite database directory is invalid.");
        }

        Directory.CreateDirectory(directory);

        await using var connection =
            new SqliteConnection(
                ConnectionString);

        await connection.OpenAsync(
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            PRAGMA journal_mode = WAL;
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS Device
            (
                Id              INTEGER NOT NULL
                                PRIMARY KEY
                                CHECK (Id = 1),

                DeviceId        TEXT NOT NULL UNIQUE,
                MachineName     TEXT NOT NULL,
                DisplayName     TEXT NOT NULL,
                Platform        TEXT NOT NULL,
                InstalledAtUtc  TEXT NOT NULL,
                UpdatedAtUtc    TEXT NOT NULL
            );
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS AdminUser
            (
                Id              INTEGER NOT NULL
                                PRIMARY KEY AUTOINCREMENT,

                Username        TEXT NOT NULL
                                COLLATE NOCASE
                                UNIQUE,

                PasswordHash    TEXT NOT NULL,
                CreatedAtUtc    TEXT NOT NULL,
                UpdatedAtUtc    TEXT NOT NULL
            );
            """,
            cancellationToken);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS RecoveryCredential
            (
                AdminUserId      INTEGER NOT NULL PRIMARY KEY,
                RecoveryCodeHash TEXT NOT NULL,
                CreatedAtUtc     TEXT NOT NULL,
                UsedAtUtc        TEXT NULL,

                FOREIGN KEY(AdminUserId)
                    REFERENCES AdminUser(Id)
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS ServiceState
            (
                ServiceId       TEXT NOT NULL PRIMARY KEY,
                BaseState       INTEGER NOT NULL,
                UpdatedAtUtc    TEXT NOT NULL
            );
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS TemporaryGrant
            (
                ServiceId       TEXT NOT NULL PRIMARY KEY,
                GrantedAtUtc    TEXT NOT NULL,
                ExpiresAtUtc    TEXT NOT NULL
            );
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS AuditEvent
            (
                Id              INTEGER NOT NULL
                                PRIMARY KEY AUTOINCREMENT,

                ServiceId       TEXT NULL,
                EventType       TEXT NOT NULL,
                Details         TEXT NULL,
                CreatedAtUtc    TEXT NOT NULL
            );
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS
                IX_AuditEvent_CreatedAtUtc
            ON AuditEvent(CreatedAtUtc);
            """,
            cancellationToken);

        await ExecuteAsync(
            connection,
            """
            PRAGMA user_version = 4;
            """,
            cancellationToken);

        _logger.LogInformation(
            "WebControl SQLite database initialized at {DatabasePath}.",
            DatabasePath);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}


