namespace WebControl.Proxy.Models;

public sealed class ProxyOptions
{
    public string ListenAddress { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 8877;

    public int Backlog { get; init; } = 512;

    public TimeSpan ConnectTimeout { get; init; } =
        TimeSpan.FromSeconds(15);
}
