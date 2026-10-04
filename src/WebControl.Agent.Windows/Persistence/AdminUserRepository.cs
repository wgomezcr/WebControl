using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WebControl.Agent.Windows.Persistence;

public sealed class AdminUserRepository
{
    private readonly WebControlDatabase _database;

    public AdminUserRepository(
        WebControlDatabase database)
    {
        _database = database;
    }

    public async Task<bool> HasAnyAsync(
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
            SELECT EXISTS
            (
                SELECT 1
                FROM AdminUser
                LIMIT 1
            );
            """;

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt32(
            result,
            CultureInfo.InvariantCulture) == 1;
    }
    public async Task<AdminUserRecord?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

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
                Id,
                Username,
                PasswordHash,
                CreatedAtUtc,
                UpdatedAtUtc
            FROM AdminUser
            WHERE Username = $username
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$username",
            username.Trim());

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        return new AdminUserRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),

            DateTimeOffset.Parse(
                reader.GetString(3),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),

            DateTimeOffset.Parse(
                reader.GetString(4),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
    }

    public async Task<long> CreateAsync(
        string username,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Username cannot be empty.",
                nameof(username));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException(
                "Password hash cannot be empty.",
                nameof(passwordHash));
        }

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
            INSERT INTO AdminUser
            (
                Username,
                PasswordHash,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                $username,
                $passwordHash,
                $createdAtUtc,
                $updatedAtUtc
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$username",
            username.Trim());

        command.Parameters.AddWithValue(
            "$passwordHash",
            passwordHash);

        command.Parameters.AddWithValue(
            "$createdAtUtc",
            now);

        command.Parameters.AddWithValue(
            "$updatedAtUtc",
            now);

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt64(
            result,
            CultureInfo.InvariantCulture);
    }

    public async Task UpdatePasswordHashAsync(
        long userId,
        string passwordHash,
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
            UPDATE AdminUser
            SET
                PasswordHash = $passwordHash,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$passwordHash",
            passwordHash);

        command.Parameters.AddWithValue(
            "$updatedAtUtc",
            now);

        command.Parameters.AddWithValue(
            "$id",
            userId);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}

