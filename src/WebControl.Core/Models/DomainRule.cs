namespace WebControl.Core.Models;

public sealed class DomainRule
{
    public DomainRule(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException(
                "Domain pattern cannot be empty.",
                nameof(pattern));
        }

        Pattern = pattern.Trim().ToLowerInvariant();
    }

    public string Pattern { get; }
}
