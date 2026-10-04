using System.Globalization;
using Microsoft.Data.Sqlite;
using WebControl.Core.Models;

namespace WebControl.Agent.Windows.Persistence;

public sealed class WebControlStateRepository
{
    private readonly WebControlDatabase _database;

    public WebControlStateRepository(
        WebControlDatabase database)
    {
        _database = database;
    }

    public async Task<ServiceAccessState?> GetServiceStateAsync(
        string serviceId,
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
            SELECT BaseState
            FROM ServiceState
            WHERE ServiceId = $serviceId;
            """;

        command.Parameters.AddWithValue(
            "$serviceId",
            serviceId);

        var value =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (value is null ||
            value is DBNull)
        {
            return null;
        }

        return (ServiceAccessState)
            Convert.ToInt32(
                value,
                CultureInfo.InvariantCulture);
    }

    public async Task UpsertServiceStateAsync(
        string serviceId,
        ServiceAccessState state,
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
            INSERT INTO ServiceState
            (
                ServiceId,
                BaseState,
                UpdatedAtUtc
            )
            VALUES
            (
                $serviceId,
                $baseState,
                $updatedAtUtc
            )
            ON CONFLICT(ServiceId)
            DO UPDATE SET
                BaseState = excluded.BaseState,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        command.Parameters.AddWithValue(
            "$serviceId",
            serviceId);

        command.Parameters.AddWithValue(
            "$baseState",
            (int)state);

        command.Parameters.AddWithValue(
            "$updatedAtUtc",
            DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task<TemporaryGrant?> GetTemporaryGrantAsync(
        string serviceId,
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
                GrantedAtUtc,
                ExpiresAtUtc
            FROM TemporaryGrant
            WHERE ServiceId = $serviceId;
            """;

        command.Parameters.AddWithValue(
            "$serviceId",
            serviceId);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        var grantedAtUtc =
            DateTimeOffset.Parse(
                reader.GetString(0),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        var expiresAtUtc =
            DateTimeOffset.Parse(
                reader.GetString(1),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        return new TemporaryGrant(
            serviceId,
            grantedAtUtc,
            expiresAtUtc);
    }

    public async Task UpsertTemporaryGrantAsync(
        TemporaryGrant grant,
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
            INSERT INTO TemporaryGrant
            (
                ServiceId,
                GrantedAtUtc,
                ExpiresAtUtc
            )
            VALUES
            (
                $serviceId,
                $grantedAtUtc,
                $expiresAtUtc
            )
            ON CONFLICT(ServiceId)
            DO UPDATE SET
                GrantedAtUtc = excluded.GrantedAtUtc,
                ExpiresAtUtc = excluded.ExpiresAtUtc;
            """;

        command.Parameters.AddWithValue(
            "$serviceId",
            grant.ServiceId);

        command.Parameters.AddWithValue(
            "$grantedAtUtc",
            grant.GrantedAtUtc.ToString("O"));

        command.Parameters.AddWithValue(
            "$expiresAtUtc",
            grant.ExpiresAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    public async Task DeleteTemporaryGrantAsync(
        string serviceId,
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
            DELETE FROM TemporaryGrant
            WHERE ServiceId = $serviceId;
            """;

        command.Parameters.AddWithValue(
            "$serviceId",
            serviceId);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}
