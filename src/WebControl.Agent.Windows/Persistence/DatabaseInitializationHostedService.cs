namespace WebControl.Agent.Windows.Persistence;

public sealed class DatabaseInitializationHostedService
    : IHostedService
{
    private readonly WebControlDatabase _database;

    public DatabaseInitializationHostedService(
        WebControlDatabase database)
    {
        _database = database;
    }

    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        return _database.InitializeAsync(
            cancellationToken);
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
