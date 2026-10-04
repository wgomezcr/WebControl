using WebControl.Agent.Windows.Persistence;
using WebControl.Core.Models;
using WebControl.Core.Services;

namespace WebControl.Agent.Windows.Services;

public sealed class StateInitializationHostedService
    : IHostedService
{
    private const string YouTubeServiceId =
        "youtube";

    private readonly RuleEngine _ruleEngine;
    private readonly WebControlStateRepository _repository;
    private readonly ILogger<StateInitializationHostedService> _logger;

    public StateInitializationHostedService(
        RuleEngine ruleEngine,
        WebControlStateRepository repository,
        ILogger<StateInitializationHostedService> logger)
    {
        _ruleEngine = ruleEngine;
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var state =
            await _repository.GetServiceStateAsync(
                YouTubeServiceId,
                cancellationToken);

        if (state is null)
        {
            state =
                ServiceAccessState.Blocked;

            await _repository.UpsertServiceStateAsync(
                YouTubeServiceId,
                state.Value,
                cancellationToken);
        }

        _ruleEngine.SetState(
            YouTubeServiceId,
            state.Value);

        var grant =
            await _repository.GetTemporaryGrantAsync(
                YouTubeServiceId,
                cancellationToken);

        if (grant is not null)
        {
            var now =
                DateTimeOffset.UtcNow;

            if (state == ServiceAccessState.Blocked &&
                grant.IsActive(now))
            {
                var duration =
                    grant.ExpiresAtUtc -
                    grant.GrantedAtUtc;

                _ruleEngine.GrantTemporaryAccess(
                    YouTubeServiceId,
                    duration,
                    grant.GrantedAtUtc);

                _logger.LogInformation(
                    "Restored temporary grant for {ServiceId} until {ExpiresAtUtc}.",
                    YouTubeServiceId,
                    grant.ExpiresAtUtc);
            }
            else
            {
                await _repository.DeleteTemporaryGrantAsync(
                    YouTubeServiceId,
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Restored state for {ServiceId}: {State}.",
            YouTubeServiceId,
            state);
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
