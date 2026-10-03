using System.Collections.Concurrent;
using WebControl.Core.Models;

namespace WebControl.Core.Services;

public sealed class RuleEngine
{
    private readonly DomainMatcher _domainMatcher;

    private readonly IReadOnlyDictionary<string, ServiceDefinition>
        _services;

    private readonly ConcurrentDictionary<string, ServiceAccessState>
        _states = new();

    private readonly ConcurrentDictionary<string, TemporaryGrant>
        _temporaryGrants = new();

    public RuleEngine(
        IEnumerable<ServiceDefinition> services,
        DomainMatcher? domainMatcher = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _domainMatcher = domainMatcher ?? new DomainMatcher();

        var serviceList = services.ToArray();

        _services = serviceList.ToDictionary(
            service => service.Id,
            StringComparer.OrdinalIgnoreCase);

        foreach (var service in serviceList)
        {
            _states[service.Id] = ServiceAccessState.Allowed;
        }
    }

    public IReadOnlyCollection<ServiceDefinition> Services
        => _services.Values.ToArray();

    public void SetState(
        string serviceId,
        ServiceAccessState state)
    {
        var service = GetService(serviceId);

        _states[service.Id] = state;

        if (state == ServiceAccessState.Allowed)
        {
            _temporaryGrants.TryRemove(
                service.Id,
                out _);
        }
    }

    public ServiceAccessState GetState(string serviceId)
    {
        var service = GetService(serviceId);

        return _states[service.Id];
    }

    public TemporaryGrant GrantTemporaryAccess(
        string serviceId,
        TimeSpan duration,
        DateTimeOffset? nowUtc = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Duration must be greater than zero.");
        }

        var service = GetService(serviceId);

        var now = nowUtc ?? DateTimeOffset.UtcNow;

        var grant = new TemporaryGrant(
            service.Id,
            now,
            now.Add(duration));

        _temporaryGrants[service.Id] = grant;

        return grant;
    }

    public void RevokeTemporaryAccess(string serviceId)
    {
        var service = GetService(serviceId);

        _temporaryGrants.TryRemove(
            service.Id,
            out _);
    }

    public TemporaryGrant? GetTemporaryGrant(
        string serviceId,
        DateTimeOffset? nowUtc = null)
    {
        var service = GetService(serviceId);

        if (!_temporaryGrants.TryGetValue(
                service.Id,
                out var grant))
        {
            return null;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;

        if (grant.IsActive(now))
        {
            return grant;
        }

        _temporaryGrants.TryRemove(
            service.Id,
            out _);

        return null;
    }

    public ServiceDefinition? FindServiceByHost(
        string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        foreach (var service in _services.Values)
        {
            if (_domainMatcher.IsMatch(
                    host,
                    service.Domains))
            {
                return service;
            }
        }

        return null;
    }

    public bool IsBlocked(
        string host,
        DateTimeOffset? nowUtc = null)
    {
        var service = FindServiceByHost(host);

        if (service is null)
        {
            return false;
        }

        if (_states[service.Id] ==
            ServiceAccessState.Allowed)
        {
            return false;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;

        var temporaryGrant =
            GetTemporaryGrant(
                service.Id,
                now);

        return temporaryGrant is null;
    }

    private ServiceDefinition GetService(
        string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new ArgumentException(
                "Service id cannot be empty.",
                nameof(serviceId));
        }

        if (!_services.TryGetValue(
                serviceId.Trim(),
                out var service))
        {
            throw new KeyNotFoundException(
                $"Service '{serviceId}' was not found.");
        }

        return service;
    }
}
