namespace WebControl.Proxy.Models;

public sealed class ProxyRequest
{
    public required string Method { get; init; }

    public required string Target { get; init; }

    public required string Protocol { get; init; }

    public required string Host { get; init; }

    public required int Port { get; init; }

    public bool IsConnect =>
        Method.Equals(
            "CONNECT",
            StringComparison.OrdinalIgnoreCase);
}
