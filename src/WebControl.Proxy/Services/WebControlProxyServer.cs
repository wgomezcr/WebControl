using System.Net;
using System.Net.Sockets;
using System.Text;
using WebControl.Core.Services;
using WebControl.Proxy.Models;

namespace WebControl.Proxy.Services;

public sealed class WebControlProxyServer
{
    private readonly RuleEngine _ruleEngine;
    private readonly ProxyOptions _options;
    private readonly ProxyRequestParser _requestParser;
    private readonly HttpHeaderReader _headerReader;

    private TcpListener? _listener;

    public WebControlProxyServer(
        RuleEngine ruleEngine,
        ProxyOptions? options = null,
        ProxyRequestParser? requestParser = null,
        HttpHeaderReader? headerReader = null)
    {
        _ruleEngine =
            ruleEngine
            ?? throw new ArgumentNullException(
                nameof(ruleEngine));

        _options =
            options
            ?? new ProxyOptions();

        _requestParser =
            requestParser
            ?? new ProxyRequestParser();

        _headerReader =
            headerReader
            ?? new HttpHeaderReader();
    }

    public bool IsRunning =>
        _listener is not null;

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        if (_listener is not null)
        {
            throw new InvalidOperationException(
                "Proxy server is already running.");
        }

        var address =
            IPAddress.Parse(
                _options.ListenAddress);

        _listener =
            new TcpListener(
                address,
                _options.Port);

        _listener.Start(
            _options.Backlog);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client =
                        await _listener.AcceptTcpClientAsync(
                            cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = HandleClientAsync(
                    client,
                    cancellationToken);
            }
        }
        finally
        {
            Stop();
        }
    }

    public void Stop()
    {
        try
        {
            _listener?.Stop();
        }
        finally
        {
            _listener = null;
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            client.NoDelay = true;

            try
            {
                await using var clientStream =
                    client.GetStream();

                var header =
                    await _headerReader.ReadAsync(
                        clientStream,
                        cancellationToken);

                if (string.IsNullOrWhiteSpace(header))
                {
                    return;
                }

                var request =
                    _requestParser.Parse(header);

                if (_ruleEngine.IsBlocked(
                        request.Host))
                {
                    await SendBlockedAsync(
                        clientStream,
                        request.Host,
                        cancellationToken);

                    return;
                }

                if (request.IsConnect)
                {
                    await HandleConnectAsync(
                        request,
                        clientStream,
                        cancellationToken);

                    return;
                }

                await HandleHttpAsync(
                    request,
                    header,
                    clientStream,
                    cancellationToken);
            }
            catch (
                OperationCanceledException)
            {
            }
            catch (
                IOException)
            {
            }
            catch (
                SocketException)
            {
            }
            catch
            {
                // Logging se agregara en Agent.Windows.
            }
        }
    }

    private async Task HandleConnectAsync(
        ProxyRequest request,
        NetworkStream clientStream,
        CancellationToken cancellationToken)
    {
        using var remote =
            new TcpClient();

        remote.NoDelay = true;

        using var timeoutCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeoutCts.CancelAfter(
            _options.ConnectTimeout);

        await remote.ConnectAsync(
            request.Host,
            request.Port,
            timeoutCts.Token);

        await using var remoteStream =
            remote.GetStream();

        var response =
            Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 Connection Established\r\n" +
                "Proxy-Agent: WebControl\r\n" +
                "\r\n");

        await clientStream.WriteAsync(
            response,
            cancellationToken);

        await clientStream.FlushAsync(
            cancellationToken);

        await RelayBidirectionalAsync(
            clientStream,
            remoteStream,
            cancellationToken);
    }

    private async Task HandleHttpAsync(
        ProxyRequest request,
        string originalHeader,
        NetworkStream clientStream,
        CancellationToken cancellationToken)
    {
        using var remote =
            new TcpClient();

        remote.NoDelay = true;

        using var timeoutCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeoutCts.CancelAfter(
            _options.ConnectTimeout);

        await remote.ConnectAsync(
            request.Host,
            request.Port,
            timeoutCts.Token);

        await using var remoteStream =
            remote.GetStream();

        var forwardHeader =
            RewriteHttpHeader(
                request,
                originalHeader);

        var requestBytes =
            Encoding.ASCII.GetBytes(
                forwardHeader);

        await remoteStream.WriteAsync(
            requestBytes,
            cancellationToken);

        await remoteStream.FlushAsync(
            cancellationToken);

        await RelayBidirectionalAsync(
            clientStream,
            remoteStream,
            cancellationToken);
    }

    private static async Task RelayBidirectionalAsync(
        Stream clientStream,
        Stream remoteStream,
        CancellationToken cancellationToken)
    {
        using var relayCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        var clientToRemote =
            CopyUntilClosedAsync(
                clientStream,
                remoteStream,
                relayCts.Token);

        var remoteToClient =
            CopyUntilClosedAsync(
                remoteStream,
                clientStream,
                relayCts.Token);

        await Task.WhenAny(
            clientToRemote,
            remoteToClient);

        relayCts.Cancel();

        try
        {
            await Task.WhenAll(
                clientToRemote,
                remoteToClient);
        }
        catch (
            OperationCanceledException)
        {
        }
        catch (
            IOException)
        {
        }
    }

    private static async Task CopyUntilClosedAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer =
            new byte[64 * 1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read =
                await source.ReadAsync(
                    buffer,
                    cancellationToken);

            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(
                buffer.AsMemory(
                    0,
                    read),
                cancellationToken);

            await destination.FlushAsync(
                cancellationToken);
        }
    }

    private static string RewriteHttpHeader(
        ProxyRequest request,
        string originalHeader)
    {
        var lines =
            originalHeader.Split(
                "\r\n",
                StringSplitOptions.None);

        var first =
            lines[0].Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var target =
            request.Target;

        if (Uri.TryCreate(
                request.Target,
                UriKind.Absolute,
                out var uri))
        {
            target =
                string.IsNullOrEmpty(
                    uri.PathAndQuery)
                        ? "/"
                        : uri.PathAndQuery;
        }

        var builder =
            new StringBuilder();

        builder.Append(
            first[0]);

        builder.Append(' ');
        builder.Append(
            target);

        builder.Append(' ');
        builder.Append(
            request.Protocol);

        builder.Append("\r\n");

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith(
                    "Proxy-Connection:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.Append(line);
            builder.Append("\r\n");
        }

        builder.Append("\r\n");

        return builder.ToString();
    }

    private static async Task SendBlockedAsync(
        Stream stream,
        string host,
        CancellationToken cancellationToken)
    {
        var body =
            $"Blocked by WebControl: {host}";

        var bodyBytes =
            Encoding.UTF8.GetBytes(
                body);

        var header =
            "HTTP/1.1 403 Forbidden\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n" +
            "\r\n";

        var headerBytes =
            Encoding.ASCII.GetBytes(
                header);

        await stream.WriteAsync(
            headerBytes,
            cancellationToken);

        await stream.WriteAsync(
            bodyBytes,
            cancellationToken);

        await stream.FlushAsync(
            cancellationToken);
    }
}
