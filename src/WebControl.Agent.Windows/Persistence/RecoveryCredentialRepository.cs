using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WebControl.Agent.Windows.Persistence;

public sealed class RecoveryCredentialRepository
{
    private readonly WebControlDatabase _database;

    public RecoveryCredentialRepository(
        WebControlDatabase database)
    {
        _database = database;
    }

    public async Task<RecoveryCredentialRecord?> GetAsync(
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqliteConnection(
                _database.ConnectionString);

        await connection.OpenAsync(
            cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                AdminUserId,
                RecoveryCodeHash,
                CreatedAtUtc,
                UsedAtUtc
            FROM RecoveryCredential
            WHERE AdminUserId = $adminUserId;
            """;

        command.Parameters.AddWithValue(
            "$adminUserId",
            adminUserId);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        return new RecoveryCredentialRecord(
            reader.GetInt64(0),

            reader.GetString(1),

            DateTimeOffset.Parse(
                reader.GetString(2),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),

            reader.IsDBNull(3)
                ? null
                : DateTimeOffset.Parse(
                    reader.GetString(3),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind));
    }

    public async Task UpsertAsync(
        long adminUserId,
        string recoveryCodeHash,
        CancellationToken cancellationToken = default)
    {
        var now =
            DateTimeOffset.UtcNow.ToString("O");

        await using var connection =
            new SqliteConnection(
                _database.ConnectionString);

        await connection.OpenAsync(
            cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO RecoveryCredential
            (
                AdminUserId,
                RecoveryCodeHash,
                CreatedAtUtc,
                UsedAtUtc
            )
            VALUES
            (
                $adminUserId,
                $recoveryCodeHash,
                $createdAtUtc,
                NULL
            )
            ON CONFLICT(AdminUserId)
            DO UPDATE SET
                RecoveryCodeHash =
                    excluded.RecoveryCodeHash,

                CreatedAtUtc =
                    excluded.CreatedAtUtc,

                UsedAtUtc =
                    NULL;
            """;

        command.Parameters.AddWithValue(
            "$adminUserId",
            adminUserId);

        command.Parameters.AddWithValue(
            "$recoveryCodeHash",
            recoveryCodeHash);

        command.Parameters.AddWithValue(
            "$createdAtUtc",
            now);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task MarkUsedAsync(
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqliteConnection(
                _database.ConnectionString);

        await connection.OpenAsync(
            cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE RecoveryCredential
            SET UsedAtUtc = $usedAtUtc
            WHERE AdminUserId = $adminUserId;
            """;

        command.Parameters.AddWithValue(
            "$usedAtUtc",
            DateTimeOffset.UtcNow.ToString("O"));

        command.Parameters.AddWithValue(
            "$adminUserId",
            adminUserId);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}
