using System.Text.Json;
using WebControl.Agent.Windows.Persistence;
using WebControl.Core.Models;
using WebControl.Core.Services;

namespace WebControl.Agent.Windows.Services;

public sealed class ServiceControlService
{
    private readonly RuleEngine _ruleEngine;
    private readonly WebControlStateRepository _repository;
    private readonly WebControlAuditRepository _auditRepository;

    public ServiceControlService(
        RuleEngine ruleEngine,
        WebControlStateRepository repository,
        WebControlAuditRepository auditRepository)
    {
        _ruleEngine = ruleEngine;
        _repository = repository;
        _auditRepository = auditRepository;
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

        await _auditRepository.AddAsync(
            serviceId,
            AuditEventTypes.Blocked,
            cancellationToken: cancellationToken);
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

        await _auditRepository.AddAsync(
            serviceId,
            AuditEventTypes.Allowed,
            cancellationToken: cancellationToken);
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

        var activeGrant =
            _ruleEngine.GrantTemporaryAccess(
                serviceId,
                duration,
                now);

        var details =
            JsonSerializer.Serialize(
                new
                {
                    durationMinutes =
                        duration.TotalMinutes,

                    expiresAtUtc =
                        activeGrant.ExpiresAtUtc
                });

        await _auditRepository.AddAsync(
            serviceId,
            AuditEventTypes.TemporaryGrantCreated,
            details,
            cancellationToken);

        return activeGrant;
    }
}
