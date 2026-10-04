using WebControl.Agent.Windows.Persistence;
using WebControl.Core.Models;
using WebControl.Core.Services;

namespace WebControl.Agent.Windows.Services;

public sealed class ServiceControlService
{
    private readonly RuleEngine _ruleEngine;
    private readonly WebControlStateRepository _repository;

    public ServiceControlService(
        RuleEngine ruleEngine,
        WebControlStateRepository repository)
    {
        _ruleEngine = ruleEngine;
        _repository = repository;
    }

    public async Task SetBlockedAsync(
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpsertServiceStateAsync(
            serviceId,
            ServiceAccessState.Blocked,
            cancellationToken);

        await _repository.DeleteTemporaryGrantAsync(
            serviceId,
            cancellationToken);

        _ruleEngine.SetState(
            serviceId,
            ServiceAccessState.Blocked);

        _ruleEngine.RevokeTemporaryAccess(
            serviceId);
    }

    public async Task SetAllowedAsync(
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpsertServiceStateAsync(
            serviceId,
            ServiceAccessState.Allowed,
            cancellationToken);

        await _repository.DeleteTemporaryGrantAsync(
            serviceId,
            cancellationToken);

        _ruleEngine.SetState(
            serviceId,
            ServiceAccessState.Allowed);
    }

    public async Task<TemporaryGrant> GrantTemporaryAccessAsync(
        string serviceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var now =
            DateTimeOffset.UtcNow;

        var grant =
            new TemporaryGrant(
                serviceId,
                now,
                now.Add(duration));

        await _repository.UpsertServiceStateAsync(
            serviceId,
            ServiceAccessState.Blocked,
            cancellationToken);

        await _repository.UpsertTemporaryGrantAsync(
            grant,
            cancellationToken);

        _ruleEngine.SetState(
            serviceId,
            ServiceAccessState.Blocked);

        return _ruleEngine.GrantTemporaryAccess(
            serviceId,
            duration,
            now);
    }
}
