using WebControl.Proxy.Services;

namespace WebControl.Tests;

public sealed class ProxyRequestParserTests
{
    private readonly ProxyRequestParser _parser =
        new();

    [Fact]
    public void ConnectRequest_ParsesHostAndPort()
    {
        var request =
            _parser.Parse(
                "CONNECT www.youtube.com:443 HTTP/1.1\r\n" +
                "Host: www.youtube.com:443\r\n" +
                "\r\n");

        Assert.True(
            request.IsConnect);

        Assert.Equal(
            "www.youtube.com",
            request.Host);

        Assert.Equal(
            443,
            request.Port);
    }

    [Fact]
    public void ConnectWithoutPort_Uses443()
    {
        var request =
            _parser.Parse(
                "CONNECT youtube.com HTTP/1.1\r\n" +
                "Host: youtube.com\r\n" +
                "\r\n");

        Assert.Equal(
            "youtube.com",
            request.Host);

        Assert.Equal(
            443,
            request.Port);
    }

    [Fact]
    public void AbsoluteHttpUrl_ParsesHostAndPort()
    {
        var request =
            _parser.Parse(
                "GET http://example.com/test?q=1 HTTP/1.1\r\n" +
                "Host: example.com\r\n" +
                "\r\n");

        Assert.False(
            request.IsConnect);

        Assert.Equal(
            "example.com",
            request.Host);

        Assert.Equal(
            80,
            request.Port);
    }

    [Fact]
    public void HostHeaderWithPort_IsParsed()
    {
        var request =
            _parser.Parse(
                "GET /test HTTP/1.1\r\n" +
                "Host: example.com:8080\r\n" +
                "\r\n");

        Assert.Equal(
            "example.com",
            request.Host);

        Assert.Equal(
            8080,
            request.Port);
    }

    [Fact]
    public void MissingHost_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => _parser.Parse(
                "GET /test HTTP/1.1\r\n" +
                "\r\n"));
    }
}
