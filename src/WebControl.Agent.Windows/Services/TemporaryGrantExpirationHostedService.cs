using System.Text.Json;
using WebControl.Agent.Windows.Persistence;
using WebControl.Core.Services;

namespace WebControl.Agent.Windows.Services;

public sealed class TemporaryGrantExpirationHostedService
    : BackgroundService
{
    private const string YouTubeServiceId =
        "youtube";

    private readonly RuleEngine _ruleEngine;
    private readonly WebControlStateRepository _repository;
    private readonly WebControlAuditRepository _auditRepository;
    private readonly ILogger<TemporaryGrantExpirationHostedService> _logger;

    public TemporaryGrantExpirationHostedService(
        RuleEngine ruleEngine,
        WebControlStateRepository repository,
        WebControlAuditRepository auditRepository,
        ILogger<TemporaryGrantExpirationHostedService> logger)
    {
        _ruleEngine = ruleEngine;
        _repository = repository;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpirationAsync(
                    stoppingToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CheckExpirationAsync(
        CancellationToken cancellationToken)
    {
        var grant =
            await _repository.GetTemporaryGrantAsync(
                YouTubeServiceId,
                cancellationToken);

        if (grant is null)
        {
            return;
        }

        if (grant.IsActive(
                DateTimeOffset.UtcNow))
        {
            return;
        }

        await _repository.DeleteTemporaryGrantAsync(
            YouTubeServiceId,
            cancellationToken);

        _ruleEngine.RevokeTemporaryAccess(
            YouTubeServiceId);

        var details =
            JsonSerializer.Serialize(
                new
                {
                    expiresAtUtc =
                        grant.ExpiresAtUtc
                });

        await _auditRepository.AddAsync(
            YouTubeServiceId,
            AuditEventTypes.TemporaryGrantExpired,
            details,
            cancellationToken);

        _logger.LogInformation(
            "Temporary grant expired for {ServiceId}.",
            YouTubeServiceId);
    }
}
