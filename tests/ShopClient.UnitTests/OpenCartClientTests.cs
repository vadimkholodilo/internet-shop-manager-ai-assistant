using System.Net;
using System.Net.Sockets;
using ShopClient.OpenCart;

namespace ShopClient.UnitTests;

/// <summary>How the client reports a shop that cannot be reached, using real sockets (no shop and no stand-in for HTTP).</summary>
public sealed class OpenCartClientTests
{
    private static readonly CatalogCategory Category = new("c", "Категория", null);

    private static Uri UrlOf(TcpListener listener) => new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");

    [Fact]
    public async Task AShopThatStopsAnsweringEndsTheRunWithAClearError()
    {
        // The operating system completes the connection for a listening socket, but nothing ever answers the request.
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        using var client = new OpenCartClient(new ShopConnection(UrlOf(silent), "key") { RequestTimeout = TimeSpan.FromMilliseconds(300) });

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("did not answer", error.Message);
    }

    [Fact]
    public async Task ALostConnectionEndsTheRunWithAClearError()
    {
        using var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var url = UrlOf(closed);
        closed.Stop();
        using var client = new OpenCartClient(new ShopConnection(url, "key"));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("Lost connection", error.Message);
    }

    [Fact]
    public async Task CancellationByTheCallerIsNotReportedAsAnUnavailableShop()
    {
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        using var client = new OpenCartClient(new ShopConnection(UrlOf(silent), "key"));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UpsertCategoryAsync(Category, cancelled.Token));
    }

    [Fact]
    public async Task WaitingForAShopThatHangsGivesUpWithinTheOverallTimeout()
    {
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        using var client = new OpenCartClient(new ShopConnection(UrlOf(silent), "key") { RequestTimeout = TimeSpan.FromSeconds(30) });
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAsync<ShopUnavailableException>(() => client.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(500)));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");
    }

    [Fact]
    public async Task AnUnreadableImageFileIsReportedAsACatalogProblemOfThatProduct()
    {
        using var client = new OpenCartClient(new ShopConnection(new Uri("http://127.0.0.1:9"), "key"));

        var error = await Assert.ThrowsAsync<CatalogException>(() => client.UploadImageAsync("sku", "/no/such/photo.png"));

        Assert.IsAssignableFrom<IOException>(error.InnerException);
    }

    [Fact(Timeout = 15_000)]
    public async Task AGatewayErrorEndsTheRunInsteadOfFailingEveryRecord()
    {
        using var shop = new CannedShop("HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key"));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
    }

    [Fact(Timeout = 15_000)]
    public async Task ARejectionCarriesTheStatusAndTheShopsMessage()
    {
        const string body = "{\"error\":\"duplicate keyword\"}";
        using var shop = new CannedShop($"HTTP/1.1 409 Conflict\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key"));

        var error = await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Contains("duplicate keyword", error.Message);
    }

    [Fact(Timeout = 15_000)]
    public async Task ASuccessAnswerThatIsNotJsonIsACatalogProblemNotACrash()
    {
        const string body = "<html>proxy</html>";
        using var shop = new CannedShop($"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key"));

        await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));
    }

    [Fact]
    public async Task BadArgumentsFailBeforeAnyRequest()
    {
        using var client = new OpenCartClient(new ShopConnection(new Uri("http://127.0.0.1:9"), "key"));
        var product = new CatalogProduct("sku", "n", "c", "b", "d", new Dictionary<string, string>(), 1m, 1);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.UpsertCategoryAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertCategoryAsync(Category with { Slug = " " }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.UpsertProductAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertProductAsync(product with { Attributes = null! }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.UpsertProductAsync(product with { Quantity = -1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UploadImageAsync("", "a.png"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WaitUntilReadyAsync(TimeSpan.FromSeconds(-1)));
    }

    /// <summary>A real socket that answers every request with one fixed raw HTTP response.</summary>
    private sealed class CannedShop : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);

        public CannedShop(string response)
        {
            listener.Start();
            Url = UrlOf(listener);
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        using var connection = await listener.AcceptTcpClientAsync();
                        var stream = connection.GetStream();
                        await ReadRequestAsync(stream);
                        await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(response));
                    }
                }
                catch (Exception e) when (e is ObjectDisposedException or SocketException or IOException)
                {
                    // The test is over and the listener was stopped.
                }
            });
        }

        public Uri Url { get; }

        public void Dispose() => listener.Stop();

        // Answering before the request is fully read would reset the connection under the client.
        private static async Task ReadRequestAsync(NetworkStream stream)
        {
            var received = new List<byte>();
            var buffer = new byte[4096];
            var total = int.MaxValue;
            while (received.Count < total)
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    return;
                }
                received.AddRange(buffer.Take(count));

                var text = System.Text.Encoding.ASCII.GetString(received.ToArray());
                var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd >= 0)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(text, @"content-length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    total = headerEnd + 4 + (match.Success ? int.Parse(match.Groups[1].Value) : 0);
                }
            }
        }
    }
}
