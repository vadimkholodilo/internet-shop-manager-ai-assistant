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
        using var client = new OpenCartClient(new ShopConnection(UrlOf(silent), "key") { RequestTimeout = TimeSpan.FromMilliseconds(300), MaxRetries = 0 });

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
        using var client = new OpenCartClient(new ShopConnection(url, "key") { MaxRetries = 0 });

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
        using var shop = new ScriptedShop(Status(503));
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key") { MaxRetries = 0 });

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
    }

    [Fact(Timeout = 15_000)]
    public async Task ARejectionCarriesTheStatusAndTheShopsMessage()
    {
        const string body = "{\"error\":\"duplicate keyword\"}";
        using var shop = new ScriptedShop(Json(409, "Conflict", body));
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key"));

        var error = await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Contains("duplicate keyword", error.Message);
    }

    [Fact(Timeout = 15_000)]
    public async Task ASuccessAnswerThatIsNotJsonIsACatalogProblemNotACrash()
    {
        const string body = "<html>proxy</html>";
        using var shop = new ScriptedShop($"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
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

    private static string Status(int code, string headers = "") =>
        $"HTTP/1.1 {code} X\r\n{headers}Content-Length: 0\r\nConnection: close\r\n\r\n";

    private static string Json(int code, string reason, string body) =>
        $"HTTP/1.1 {code} {reason}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";

    private static ShopConnection Quick(ScriptedShop shop, int maxRetries = 3) =>
        new(shop.Url, "key") { MaxRetries = maxRetries, RetryDelay = TimeSpan.FromMilliseconds(10) };

    [Fact(Timeout = 15_000)]
    public async Task TemporaryFailuresAreRetriedUntilTheShopAnswers()
    {
        using var shop = new ScriptedShop(Status(503), Status(500), Json(200, "OK", "{\"id\":5,\"created\":true}"));
        using var client = new OpenCartClient(Quick(shop));

        var result = await client.UpsertCategoryAsync(Category);

        Assert.Equal(new UpsertResult(5, true), result);
        Assert.Equal(3, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task ALostConnectionIsRetriedAndTheFinalFailureNamesTheAttempts()
    {
        using var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var url = UrlOf(closed);
        closed.Stop();
        using var client = new OpenCartClient(new ShopConnection(url, "key") { MaxRetries = 2, RetryDelay = TimeSpan.FromMilliseconds(10) });

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("after 3 attempts", error.Message);
        Assert.IsAssignableFrom<HttpRequestException>(error.InnerException);
    }

    [Fact(Timeout = 15_000)]
    public async Task ARepeatedlyUnavailableShopFailsAfterTheConfiguredAttempts()
    {
        using var shop = new ScriptedShop(Status(503));
        using var client = new OpenCartClient(Quick(shop, maxRetries: 2));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("after 3 attempts", error.Message);
        Assert.Equal(3, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task APersistentInternalErrorIsACatalogFailureAfterTheRetries()
    {
        using var shop = new ScriptedShop(Json(500, "Internal Server Error", "{\"error\":\"Internal error\"}"));
        using var client = new OpenCartClient(Quick(shop, maxRetries: 1));

        var error = await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("after 2 attempts", error.Message);
        Assert.Equal(2, shop.Requests);
    }

    [Theory(Timeout = 15_000)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(415)]
    public async Task ARejectedRecordIsNotRetried(int code)
    {
        using var shop = new ScriptedShop(Status(code));
        using var client = new OpenCartClient(Quick(shop));

        await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(1, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task ARejectedApiKeyIsNotRetried()
    {
        using var shop = new ScriptedShop(Status(401));
        using var client = new OpenCartClient(Quick(shop));

        await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(1, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task WithoutRetriesThereIsOneAttempt()
    {
        using var shop = new ScriptedShop(Status(503));
        using var client = new OpenCartClient(Quick(shop, maxRetries: 0));

        await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(1, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task RetryAfterIsHonored()
    {
        using var shop = new ScriptedShop(Status(503, "Retry-After: 1\r\n"), Json(200, "OK", "{\"id\":1}"));
        using var client = new OpenCartClient(Quick(shop));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await client.UpsertCategoryAsync(Category);

        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(900), $"waited only {clock.Elapsed}");
    }

    [Fact(Timeout = 15_000)]
    public async Task CancellingDuringAWaitBetweenAttemptsStopsRetrying()
    {
        using var shop = new ScriptedShop(Status(503));
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key") { RetryDelay = TimeSpan.FromSeconds(5) });
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UpsertCategoryAsync(Category, cancelled.Token));

        Assert.Equal(1, shop.Requests);
    }

    /// <summary>A real socket that answers the requests with fixed raw HTTP responses in order, repeating the last one.</summary>
    private sealed class ScriptedShop : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private int requests;

        public ScriptedShop(params string[] responses)
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
                        var next = Math.Min(Interlocked.Increment(ref requests) - 1, responses.Length - 1);
                        await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(responses[next]));
                    }
                }
                catch (Exception e) when (e is ObjectDisposedException or SocketException or IOException)
                {
                    // The test is over and the listener was stopped.
                }
            });
        }

        public Uri Url { get; }

        public int Requests => requests;

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
