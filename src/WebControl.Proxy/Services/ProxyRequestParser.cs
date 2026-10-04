using WebControl.Proxy.Models;

namespace WebControl.Proxy.Services;

public sealed class ProxyRequestParser
{
    public ProxyRequest Parse(
        string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new ArgumentException(
                "Proxy header cannot be empty.",
                nameof(header));
        }

        var lines = header.Split(
            "\r\n",
            StringSplitOptions.None);

        if (lines.Length == 0)
        {
            throw new InvalidOperationException(
                "Invalid proxy request.");
        }

        var firstLine = lines[0]
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        if (firstLine.Length < 3)
        {
            throw new InvalidOperationException(
                "Invalid HTTP request line.");
        }

        var method = firstLine[0];
        var target = firstLine[1];
        var protocol = firstLine[2];

        if (method.Equals(
                "CONNECT",
                StringComparison.OrdinalIgnoreCase))
        {
            var authority = ParseAuthority(
                target,
                443);

            return new ProxyRequest
            {
                Method = method,
                Target = target,
                Protocol = protocol,
                Host = authority.Host,
                Port = authority.Port
            };
        }

        if (Uri.TryCreate(
                target,
                UriKind.Absolute,
                out var uri))
        {
            return new ProxyRequest
            {
                Method = method,
                Target = target,
                Protocol = protocol,
                Host = uri.Host,
                Port = uri.IsDefaultPort
                    ? GetDefaultPort(uri.Scheme)
                    : uri.Port
            };
        }

        var hostHeader = lines
            .FirstOrDefault(
                line => line.StartsWith(
                    "Host:",
                    StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(hostHeader))
        {
            throw new InvalidOperationException(
                "Host header was not found.");
        }

        var hostValue =
            hostHeader["Host:".Length..].Trim();

        var hostAuthority =
            ParseAuthority(
                hostValue,
                80);

        return new ProxyRequest
        {
            Method = method,
            Target = target,
            Protocol = protocol,
            Host = hostAuthority.Host,
            Port = hostAuthority.Port
        };
    }

    private static int GetDefaultPort(
        string scheme)
    {
        return scheme.Equals(
            "https",
            StringComparison.OrdinalIgnoreCase)
                ? 443
                : 80;
    }

    private static (
        string Host,
        int Port
    ) ParseAuthority(
        string authority,
        int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException(
                "Invalid authority.");
        }

        authority = authority.Trim();

        if (authority.StartsWith("["))
        {
            var closing =
                authority.IndexOf(']');

            if (closing <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid IPv6 authority.");
            }

            var host =
                authority[1..closing];

            var port = defaultPort;

            if (authority.Length > closing + 1 &&
                authority[closing + 1] == ':')
            {
                port = int.Parse(
                    authority[(closing + 2)..]);
            }

            return (
                host,
                port);
        }

        var lastColon =
            authority.LastIndexOf(':');

        if (lastColon > 0 &&
            authority.IndexOf(':') == lastColon)
        {
            var portText =
                authority[(lastColon + 1)..];

            if (int.TryParse(
                    portText,
                    out var parsedPort))
            {
                return (
                    authority[..lastColon],
                    parsedPort);
            }
        }

        return (
            authority,
            defaultPort);
    }
}
