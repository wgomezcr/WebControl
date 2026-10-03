namespace WebControl.Core.Models;

public sealed class ServiceDefinition
{
    public ServiceDefinition(
        string id,
        string name,
        IEnumerable<DomainRule> domains)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Service id cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Service name cannot be empty.",
                nameof(name));
        }

        Id = id.Trim().ToLowerInvariant();
        Name = name.Trim();

        Domains = domains?.ToArray()
            ?? throw new ArgumentNullException(nameof(domains));

        if (Domains.Count == 0)
        {
            throw new ArgumentException(
                "A service must contain at least one domain.",
                nameof(domains));
        }
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<DomainRule> Domains { get; }
}
