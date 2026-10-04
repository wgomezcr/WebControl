using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WebControl.Agent.Windows.Persistence;

public sealed class WebControlAuditRepository
{
    private readonly WebControlDatabase _database;

    public WebControlAuditRepository(
        WebControlDatabase database)
    {
        _database = database;
    }

    public async Task AddAsync(
        string? serviceId,
        string eventType,
        string? details = null,
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
            INSERT INTO AuditEvent
            (
                ServiceId,
                EventType,
                Details,
                CreatedAtUtc
            )
            VALUES
            (
                $serviceId,
                $eventType,
                $details,
                $createdAtUtc
            );
            """;

        command.Parameters.AddWithValue(
            "$serviceId",
            (object?)serviceId ?? DBNull.Value);

        command.Parameters.AddWithValue(
            "$eventType",
            eventType);

        command.Parameters.AddWithValue(
            "$details",
            (object?)details ?? DBNull.Value);

        command.Parameters.AddWithValue(
            "$createdAtUtc",
            DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEventRecord>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
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
                ServiceId,
                EventType,
                Details,
                CreatedAtUtc
            FROM AuditEvent
            ORDER BY Id DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        var result =
            new List<AuditEventRecord>();

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            result.Add(
                new AuditEventRecord(
                    reader.GetInt64(0),

                    reader.IsDBNull(1)
                        ? null
                        : reader.GetString(1),

                    reader.GetString(2),

                    reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),

                    DateTimeOffset.Parse(
                        reader.GetString(4),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind)));
        }

        return result;
    }
}
