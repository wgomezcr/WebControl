using WebControl.Core.Models;

namespace WebControl.Agent.Windows.Configuration;

internal static class DefaultServiceDefinitions
{
    public static IReadOnlyCollection<ServiceDefinition> Create()
    {
        return new[]
        {
            new ServiceDefinition(
                "youtube",
                "YouTube",
                new[]
                {
                    new DomainRule("youtube.com"),
                    new DomainRule("*.youtube.com"),

                    new DomainRule("youtu.be"),
                    new DomainRule("*.youtu.be"),

                    new DomainRule("googlevideo.com"),
                    new DomainRule("*.googlevideo.com"),

                    new DomainRule("ytimg.com"),
                    new DomainRule("*.ytimg.com"),

                    new DomainRule("youtube-nocookie.com"),
                    new DomainRule("*.youtube-nocookie.com"),

                    new DomainRule("youtubei.googleapis.com")
                })
        };
    }
}
