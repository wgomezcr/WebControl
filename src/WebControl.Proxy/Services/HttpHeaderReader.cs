using System.Text;

namespace WebControl.Proxy.Services;

public sealed class HttpHeaderReader
{
    private const int MaxHeaderSize =
        64 * 1024;

    public async Task<string> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var memory =
            new MemoryStream();

        var buffer =
            new byte[1];

        var state = 0;

        while (memory.Length < MaxHeaderSize)
        {
            var read =
                await stream.ReadAsync(
                    buffer,
                    cancellationToken);

            if (read == 0)
            {
                break;
            }

            var value =
                buffer[0];

            memory.WriteByte(value);

            state = state switch
            {
                0 when value == 13 => 1,
                1 when value == 10 => 2,
                2 when value == 13 => 3,
                3 when value == 10 => 4,
                _ => value == 13 ? 1 : 0
            };

            if (state == 4)
            {
                return Encoding.ASCII.GetString(
                    memory.ToArray());
            }
        }

        if (memory.Length >= MaxHeaderSize)
        {
            throw new InvalidOperationException(
                "HTTP header exceeds maximum size.");
        }

        return Encoding.ASCII.GetString(
            memory.ToArray());
    }
}
