namespace WebControl.Agent.Windows.Persistence;

public sealed record AuditEventRecord(
    long Id,
    string? ServiceId,
    string EventType,
    string? Details,
    DateTimeOffset CreatedAtUtc);
