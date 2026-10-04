using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WebControl.Agent.Windows.Persistence;

public sealed class DeviceIdentityRepository
{
    private const long SingletonId = 1;

    private readonly WebControlDatabase _database;

    public DeviceIdentityRepository(
        WebControlDatabase database)
    {
        _database = database;
    }

    public async Task<DeviceIdentityRecord> GetOrCreateAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new SqliteConnection(
                _database.ConnectionString);

        await connection.OpenAsync(
            cancellationToken);

        var existing =
            await ReadAsync(
                connection,
                cancellationToken);

        var currentMachineName =
            Environment.MachineName;

        if (existing is not null)
        {
            if (!string.Equals(
                    existing.MachineName,
                    currentMachineName,
                    StringComparison.OrdinalIgnoreCase))
            {
                var updated =
                    existing with
                    {
                        MachineName =
                            currentMachineName,

                        UpdatedAtUtc =
                            DateTimeOffset.UtcNow
                    };

                await UpdateMachineNameAsync(
                    connection,
                    updated,
                    cancellationToken);

                return updated;
            }

            return existing;
        }

        var now =
            DateTimeOffset.UtcNow;

        var created =
            new DeviceIdentityRecord(
                Guid.NewGuid(),
                currentMachineName,
                currentMachineName,
                "Windows",
                now,
                now);

        await InsertAsync(
            connection,
            created,
            cancellationToken);

        return created;
    }

    private static async Task<DeviceIdentityRecord?> ReadAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                DeviceId,
                MachineName,
                DisplayName,
                Platform,
                InstalledAtUtc,
                UpdatedAtUtc
            FROM Device
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            SingletonId);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(
                cancellationToken))
        {
            return null;
        }

        return new DeviceIdentityRecord(
            Guid.Parse(
                reader.GetString(0)),

            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),

            DateTimeOffset.Parse(
                reader.GetString(4),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),

            DateTimeOffset.Parse(
                reader.GetString(5),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
    }

    private static async Task InsertAsync(
        SqliteConnection connection,
        DeviceIdentityRecord device,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Device
            (
                Id,
                DeviceId,
                MachineName,
                DisplayName,
                Platform,
                InstalledAtUtc,
                UpdatedAtUtc
            )
            VALUES
            (
                $id,
                $deviceId,
                $machineName,
                $displayName,
                $platform,
                $installedAtUtc,
                $updatedAtUtc
            );
            """;

        command.Parameters.AddWithValue(
            "$id",
            SingletonId);

        command.Parameters.AddWithValue(
            "$deviceId",
            device.DeviceId.ToString());

        command.Parameters.AddWithValue(
            "$machineName",
            device.MachineName);

        command.Parameters.AddWithValue(
            "$displayName",
            device.DisplayName);

        command.Parameters.AddWithValue(
            "$platform",
            device.Platform);

        command.Parameters.AddWithValue(
            "$installedAtUtc",
            device.InstalledAtUtc.ToString("O"));

        command.Parameters.AddWithValue(
            "$updatedAtUtc",
            device.UpdatedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static async Task UpdateMachineNameAsync(
        SqliteConnection connection,
        DeviceIdentityRecord device,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Device
            SET
                MachineName = $machineName,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$machineName",
            device.MachineName);

        command.Parameters.AddWithValue(
            "$updatedAtUtc",
            device.UpdatedAtUtc.ToString("O"));

        command.Parameters.AddWithValue(
            "$id",
            SingletonId);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}
