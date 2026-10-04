using WebControl.Proxy.Services;

namespace WebControl.Agent.Windows.Services;

public sealed class WebControlProxyHostedService : BackgroundService
{
    private readonly WebControlProxyServer _proxyServer;
    private readonly ILogger<WebControlProxyHostedService> _logger;

    public WebControlProxyHostedService(
        WebControlProxyServer proxyServer,
        ILogger<WebControlProxyHostedService> logger)
    {
        _proxyServer = proxyServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting WebControl proxy on 127.0.0.1:8877.");

        try
        {
            await _proxyServer.RunAsync(
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation(
            "WebControl proxy stopped.");
    }
}
