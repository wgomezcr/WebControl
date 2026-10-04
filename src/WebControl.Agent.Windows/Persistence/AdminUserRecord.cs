namespace WebControl.Agent.Windows.Persistence;

public sealed record AdminUserRecord(
    long Id,
    string Username,
    string PasswordHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
