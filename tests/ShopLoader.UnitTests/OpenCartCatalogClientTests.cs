using ShopLoader.Models;
using ShopLoader.OpenCart;

namespace ShopLoader.UnitTests;

public sealed class OpenCartCatalogClientTests
{
    private sealed class FailingHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw failure;
    }

    private static OpenCartCatalogClient ClientFailingWith(Exception failure) =>
        new(new Uri("http://shop"), "key", new FailingHandler(failure));

    private static readonly CategoryRecord Category = new("c", "Категория", null);

    [Fact]
    public async Task AShopThatStopsAnsweringEndsTheRunWithAClearError()
    {
        // HttpClient reports its own timeout as a TaskCanceledException while the caller's token is not cancelled.
        using var client = ClientFailingWith(new TaskCanceledException("timed out"));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category, CancellationToken.None));

        Assert.Contains("did not answer", error.Message);
    }

    [Fact]
    public async Task ALostConnectionEndsTheRunWithAClearError()
    {
        using var client = ClientFailingWith(new HttpRequestException("connection refused"));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category, CancellationToken.None));

        Assert.Contains("Lost connection", error.Message);
    }

    [Fact]
    public async Task CancellationByTheCallerIsNotReportedAsAnUnavailableShop()
    {
        using var client = ClientFailingWith(new TaskCanceledException());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.UpsertCategoryAsync(Category, cancelled.Token));
    }
}
