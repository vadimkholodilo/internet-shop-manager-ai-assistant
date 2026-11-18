using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ShopClient.UnitTests;

/// <summary>
/// A real socket that answers the requests with fixed raw HTTP responses in order, repeating the last one, or never
/// answers when given none. It is no shop: it only produces answers (503, Retry-After, an HTML page) that the real shop
/// cannot be made to give on demand, to test the client's retry and error mapping.
/// </summary>
public sealed class ScriptedShop : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopped = new();
    private int requests;

    public ScriptedShop(params string[] responses)
    {
        listener.Start();
        Url = UnitTest.UrlOf(listener);
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using var connection = await listener.AcceptTcpClientAsync();
                    if (responses.Length == 0)
                    {
                        // The operating system completes the connection, but nothing ever answers the request.
                        await Task.Delay(Timeout.Infinite, stopped.Token);
                    }

                    var stream = connection.GetStream();
                    await ReadRequestAsync(stream);
                    var next = Math.Min(Interlocked.Increment(ref requests) - 1, responses.Length - 1);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(responses[next]));
                }
            }
            catch (Exception e) when (e is ObjectDisposedException or SocketException or IOException or OperationCanceledException)
            {
                // The test is over and the listener was stopped.
            }
        });
    }

    public Uri Url { get; }

    public int Requests => requests;

    public void Dispose()
    {
        stopped.Cancel();
        listener.Stop();
    }

    // Answering before the request is fully read would reset the connection under the client.
    private static async Task ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var request = new MemoryStream();
        var total = long.MaxValue;
        while (request.Length < total)
        {
            var count = await stream.ReadAsync(buffer);
            if (count == 0)
            {
                return;
            }
            request.Write(buffer, 0, count);

            var headerEnd = request.GetBuffer().AsSpan(0, (int)request.Length).IndexOf("\r\n\r\n"u8);
            if (total == long.MaxValue && headerEnd >= 0)
            {
                total = headerEnd + 4 + ContentLength(Encoding.ASCII.GetString(request.GetBuffer(), 0, headerEnd));
            }
        }
    }

    private static int ContentLength(string headers)
    {
        const string name = "Content-Length:";
        var line = headers.Split("\r\n").FirstOrDefault(line => line.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        return line is null ? 0 : int.Parse(line[name.Length..]);
    }
}
