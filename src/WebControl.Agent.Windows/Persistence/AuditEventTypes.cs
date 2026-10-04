namespace WebControl.Agent.Windows.Persistence;

public static class AuditEventTypes
{
    public const string Allowed =
        "Allowed";

    public const string Blocked =
        "Blocked";

    public const string TemporaryGrantCreated =
        "TemporaryGrantCreated";

    public const string TemporaryGrantRestored =
        "TemporaryGrantRestored";

    public const string TemporaryGrantExpired =
        "TemporaryGrantExpired";
}
