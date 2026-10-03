using WebControl.Core.Models;
using WebControl.Core.Services;

namespace WebControl.Tests;

public sealed class DomainMatcherTests
{
    private readonly DomainMatcher _matcher = new();

    [Theory]
    [InlineData("youtube.com", "youtube.com", true)]
    [InlineData("YOUTUBE.COM", "youtube.com", true)]
    [InlineData("youtube.com.", "youtube.com", true)]
    [InlineData("youtube.com:443", "youtube.com", true)]
    [InlineData("www.youtube.com", "youtube.com", false)]
    [InlineData("www.youtube.com", "*.youtube.com", true)]
    [InlineData("music.youtube.com", "*.youtube.com", true)]
    [InlineData("youtube.com", "*.youtube.com", false)]
    [InlineData("notyoutube.com", "*.youtube.com", false)]
    [InlineData("lichess.org", "*.youtube.com", false)]
    public void IsMatch_ReturnsExpectedResult(
        string host,
        string pattern,
        bool expected)
    {
        var result = _matcher.IsMatch(
            host,
            pattern);

        Assert.Equal(
            expected,
            result);
    }
}
