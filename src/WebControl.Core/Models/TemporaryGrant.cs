namespace WebControl.Core.Models;

public sealed class TemporaryGrant
{
    public TemporaryGrant(
        string serviceId,
        DateTimeOffset grantedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new ArgumentException(
                "Service id cannot be empty.",
                nameof(serviceId));
        }

        if (expiresAtUtc <= grantedAtUtc)
        {
            throw new ArgumentException(
                "Expiration must be later than grant time.",
                nameof(expiresAtUtc));
        }

        ServiceId = serviceId.Trim().ToLowerInvariant();
        GrantedAtUtc = grantedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string ServiceId { get; }

    public DateTimeOffset GrantedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsActive(DateTimeOffset nowUtc)
    {
        return nowUtc >= GrantedAtUtc &&
               nowUtc < ExpiresAtUtc;
    }
}
