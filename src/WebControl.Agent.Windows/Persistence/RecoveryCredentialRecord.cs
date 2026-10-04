namespace WebControl.Agent.Windows.Persistence;

public sealed record RecoveryCredentialRecord(
    long AdminUserId,
    string RecoveryCodeHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UsedAtUtc);
