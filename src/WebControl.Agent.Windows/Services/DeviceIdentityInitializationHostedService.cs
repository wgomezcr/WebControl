using WebControl.Agent.Windows.Persistence;

namespace WebControl.Agent.Windows.Services;

public sealed class DeviceIdentityInitializationHostedService
    : IHostedService
{
    private readonly DeviceIdentityRepository _repository;
    private readonly ILogger<DeviceIdentityInitializationHostedService> _logger;

    public DeviceIdentityInitializationHostedService(
        DeviceIdentityRepository repository,
        ILogger<DeviceIdentityInitializationHostedService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var device =
            await _repository.GetOrCreateAsync(
                cancellationToken);

        _logger.LogInformation(
            "WebControl device identity: {DeviceId} - {MachineName}.",
            device.DeviceId,
            device.MachineName);
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
