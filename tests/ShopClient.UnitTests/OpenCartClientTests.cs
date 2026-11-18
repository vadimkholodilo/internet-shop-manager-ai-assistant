using System.Net;
using System.Net.Sockets;
using ShopClient.OpenCart;

namespace ShopClient.UnitTests;

/// <summary>
/// How the client reports a shop that cannot be reached or answers in a temporary way, using real sockets. Most
/// scenarios need no shop at all (a closed or silent port). The <see cref="ScriptedShop"/> ones (503 then success,
/// Retry-After, an HTML answer) cannot be reproduced on demand with the real shop, so by decision of the product owner
/// they are unit tests of the client's own retry and error-mapping logic, an exception to the rule against stand-ins for
/// the shop. What the real shop does with a repeated write is covered against the real shop in <c>ShopClientTests</c>.
/// </summary>
public sealed class OpenCartClientTests : UnitTest
{
    [Fact]
    public async Task AShopThatStopsAnsweringEndsTheRunWithAClearError()
    {
        using var silent = new ScriptedShop();
        using var client = new OpenCartClient(new ShopConnection(silent.Url, "key") { RequestTimeout = TimeSpan.FromMilliseconds(300), MaxRetries = 0 });

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("did not answer", error.Message);
    }

    [Fact]
    public async Task CancellationByTheCallerIsNotReportedAsAnUnavailableShop()
    {
        using var silent = new ScriptedShop();
        using var client = new OpenCartClient(new ShopConnection(silent.Url, "key"));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UpsertCategoryAsync(Category, cancelled.Token));
    }

    [Fact]
    public async Task WaitingForAShopThatHangsGivesUpWithinTheOverallTimeout()
    {
        using var silent = new ScriptedShop();
        using var client = new OpenCartClient(new ShopConnection(silent.Url, "key") { RequestTimeout = TimeSpan.FromSeconds(30) });
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

    [Theory(Timeout = 15_000)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task AnUnavailableShopEndsTheRunAfterTheConfiguredAttemptsInsteadOfFailingEveryRecord(int maxRetries)
    {
        using var shop = new ScriptedShop(Response(503));
        using var client = new OpenCartClient(Quick(shop, maxRetries));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(maxRetries + 1, shop.Requests);
        Assert.Equal(maxRetries > 0, error.Message.Contains("attempts"));
    }

    [Fact(Timeout = 15_000)]
    public async Task ARejectionCarriesTheStatusAndTheShopsMessage()
    {
        const string body = "{\"error\":\"duplicate keyword\"}";
        using var shop = new ScriptedShop(Response(409, body));
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key"));

        var error = await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Contains("duplicate keyword", error.Message);
    }

    [Fact(Timeout = 15_000)]
    public async Task ASuccessAnswerThatIsNotJsonIsACatalogProblemNotACrash()
    {
        const string body = "<html>proxy</html>";
        using var shop = new ScriptedShop(Response(200, body, "text/html"));
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

    [Fact(Timeout = 15_000)]
    public async Task TemporaryFailuresAreRetriedUntilTheShopAnswers()
    {
        using var shop = new ScriptedShop(Response(503), Response(500), Response(200, "{\"id\":5,\"created\":true}"));
        using var client = new OpenCartClient(Quick(shop));

        var result = await client.UpsertCategoryAsync(Category);

        Assert.Equal(new UpsertResult(5, true), result);
        Assert.Equal(3, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task ALostConnectionIsRetriedAndThenEndsTheRunWithAClearError()
    {
        using var client = new OpenCartClient(new ShopConnection(ClosedPortUrl(), "key") { MaxRetries = 2, RetryDelay = TimeSpan.FromMilliseconds(10) });

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Contains("Lost connection", error.Message);
        Assert.Contains("after 3 attempts", error.Message);
        Assert.IsAssignableFrom<HttpRequestException>(error.InnerException);
    }

    [Fact(Timeout = 15_000)]
    public async Task APersistentInternalErrorIsACatalogFailureAfterTheRetries()
    {
        using var shop = new ScriptedShop(Response(500, "{\"error\":\"Internal error\"}"));
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
        using var shop = new ScriptedShop(Response(code));
        using var client = new OpenCartClient(Quick(shop));

        await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(1, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task ATlsFailureIsNotRetried()
    {
        // A port that speaks plain text where TLS is expected. An unknown host takes the same path (both are
        // HttpRequestError values the helper does not retry) but cannot be reproduced reliably: some networks answer every name.
        using var plain = new TcpListener(IPAddress.Loopback, 0);
        plain.Start();
        var accepted = 0;
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using var connection = await plain.AcceptTcpClientAsync();
                    Interlocked.Increment(ref accepted);
                    await connection.GetStream().WriteAsync("HTTP/1.1 400 not tls\r\n\r\n"u8.ToArray());
                }
            }
            catch (Exception e) when (e is ObjectDisposedException or SocketException or IOException)
            {
                // The test is over.
            }
        });
        var url = new Uri($"https://127.0.0.1:{((IPEndPoint)plain.LocalEndpoint).Port}");
        using var client = new OpenCartClient(new ShopConnection(url, "key") { RetryDelay = TimeSpan.FromSeconds(30) });

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.DoesNotContain("attempts", error.Message);
        Assert.Equal(1, accepted);
    }

    [Fact(Timeout = 15_000)]
    public async Task ARejectedApiKeyIsNotRetried()
    {
        using var shop = new ScriptedShop(Response(401));
        using var client = new OpenCartClient(Quick(shop));

        await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(1, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task RetryAfterIsHonored()
    {
        using var shop = new ScriptedShop(Response(503, headers: "Retry-After: 1\r\n"), Response(200, "{\"id\":1}"));
        using var client = new OpenCartClient(Quick(shop));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await client.UpsertCategoryAsync(Category);

        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(900), $"waited only {clock.Elapsed}");
    }

    [Fact(Timeout = 15_000)]
    public async Task AnItemBusyWithAnotherWriteIsRetriedAndThenOnlyThatRecordFails()
    {
        using var shop = new ScriptedShop(Response(423));
        using var client = new OpenCartClient(Quick(shop, maxRetries: 2));

        var error = await Assert.ThrowsAsync<CatalogException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.Locked, error.StatusCode);
        Assert.Equal(3, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task ARetryAfterBeyondTheLimitEndsTheRetrying()
    {
        using var shop = new ScriptedShop(Response(503, headers: "Retry-After: 3600\r\n"));
        using var client = new OpenCartClient(Quick(shop));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(1, shop.Requests);
    }

    [Fact(Timeout = 15_000)]
    public async Task CancellingDuringAWaitBetweenAttemptsStopsRetrying()
    {
        using var shop = new ScriptedShop(Response(503));
        using var client = new OpenCartClient(new ShopConnection(shop.Url, "key") { RetryDelay = TimeSpan.FromSeconds(5) });
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UpsertCategoryAsync(Category, cancelled.Token));

        Assert.Equal(1, shop.Requests);
    }
}
