namespace WebControl.Agent.Windows.Persistence;

public sealed record DeviceIdentityRecord(
    Guid DeviceId,
    string MachineName,
    string DisplayName,
    string Platform,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc);
