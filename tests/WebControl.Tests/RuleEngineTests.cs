using WebControl.Core.Models;
using WebControl.Core.Services;

namespace WebControl.Tests;

public sealed class RuleEngineTests
{
    private static RuleEngine CreateEngine()
    {
        var youtube = new ServiceDefinition(
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
                new DomainRule("*.ytimg.com")
            });

        return new RuleEngine(
            new[]
            {
                youtube
            });
    }

    [Theory]
    [InlineData("youtube.com")]
    [InlineData("www.youtube.com")]
    [InlineData("music.youtube.com")]
    [InlineData("youtu.be")]
    [InlineData("abc.youtu.be")]
    [InlineData("googlevideo.com")]
    [InlineData("rr1.googlevideo.com")]
    [InlineData("i.ytimg.com")]
    public void FindServiceByHost_RecognizesYoutubeDomains(
        string host)
    {
        var engine = CreateEngine();

        var service =
            engine.FindServiceByHost(host);

        Assert.NotNull(service);

        Assert.Equal(
            "youtube",
            service.Id);
    }

    [Fact]
    public void UnknownDomain_IsAllowed()
    {
        var engine = CreateEngine();

        engine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        Assert.False(
            engine.IsBlocked("lichess.org"));
    }

    [Fact]
    public void BlockedService_BlocksMatchingDomain()
    {
        var engine = CreateEngine();

        engine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        Assert.True(
            engine.IsBlocked(
                "www.youtube.com"));
    }

    [Fact]
    public void AllowedService_AllowsMatchingDomain()
    {
        var engine = CreateEngine();

        engine.SetState(
            "youtube",
            ServiceAccessState.Allowed);

        Assert.False(
            engine.IsBlocked(
                "www.youtube.com"));
    }

    [Fact]
    public void TemporaryGrant_AllowsBlockedService()
    {
        var engine = CreateEngine();

        var now =
            new DateTimeOffset(
                2026,
                10,
                3,
                18,
                0,
                0,
                TimeSpan.Zero);

        engine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        engine.GrantTemporaryAccess(
            "youtube",
            TimeSpan.FromMinutes(30),
            now);

        Assert.False(
            engine.IsBlocked(
                "www.youtube.com",
                now.AddMinutes(15)));
    }

    [Fact]
    public void ExpiredTemporaryGrant_BlocksAgain()
    {
        var engine = CreateEngine();

        var now =
            new DateTimeOffset(
                2026,
                10,
                3,
                18,
                0,
                0,
                TimeSpan.Zero);

        engine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        engine.GrantTemporaryAccess(
            "youtube",
            TimeSpan.FromMinutes(30),
            now);

        Assert.True(
            engine.IsBlocked(
                "www.youtube.com",
                now.AddMinutes(31)));
    }

    [Fact]
    public void AllowingService_RemovesTemporaryGrant()
    {
        var engine = CreateEngine();

        var now =
            DateTimeOffset.UtcNow;

        engine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        engine.GrantTemporaryAccess(
            "youtube",
            TimeSpan.FromMinutes(30),
            now);

        engine.SetState(
            "youtube",
            ServiceAccessState.Allowed);

        Assert.Null(
            engine.GetTemporaryGrant(
                "youtube",
                now));
    }
}
