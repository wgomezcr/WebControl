using WebControl.Core.Models;

namespace WebControl.Core.Services;

public sealed class DomainMatcher
{
    public bool IsMatch(string host, string pattern)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var normalizedHost = NormalizeHost(host);
        var normalizedPattern = pattern
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();

        if (normalizedPattern.StartsWith("*."))
        {
            var rootDomain = normalizedPattern[2..];

            return normalizedHost.Length > rootDomain.Length &&
                   normalizedHost.EndsWith(
                       "." + rootDomain,
                       StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            normalizedHost,
            normalizedPattern,
            StringComparison.OrdinalIgnoreCase);
    }

    public bool IsMatch(
        string host,
        IEnumerable<DomainRule> rules)
    {
        return rules.Any(
            rule => IsMatch(host, rule.Pattern));
    }

    private static string NormalizeHost(string host)
    {
        var normalized = host
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();

        var colonIndex = normalized.LastIndexOf(':');

        if (colonIndex > 0 &&
            normalized.Count(character => character == ':') == 1)
        {
            normalized = normalized[..colonIndex];
        }

        return normalized;
    }
}
