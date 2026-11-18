using System.Net;
using System.Net.Sockets;
using ShopLoader.Models;
using ShopLoader.OpenCart;

namespace ShopLoader.UnitTests;

/// <summary>How the client reports a shop that cannot be reached, using real sockets (no shop and no stand-in for HTTP).</summary>
public sealed class OpenCartCatalogClientTests
{
    private static readonly CategoryRecord Category = new("c", "Категория", null);

    private static Uri UrlOf(TcpListener listener) => new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");

    [Fact]
    public async Task AShopThatStopsAnsweringEndsTheRunWithAClearError()
    {
        // The operating system completes the connection for a listening socket, but nothing ever answers the request.
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        using var client = new OpenCartCatalogClient(UrlOf(silent), "key", TimeSpan.FromMilliseconds(300));

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category, CancellationToken.None));

        Assert.Contains("did not answer", error.Message);
    }

    [Fact]
    public async Task ALostConnectionEndsTheRunWithAClearError()
    {
        using var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var url = UrlOf(closed);
        closed.Stop();
        using var client = new OpenCartCatalogClient(url, "key");

        var error = await Assert.ThrowsAsync<ShopUnavailableException>(() => client.UpsertCategoryAsync(Category, CancellationToken.None));

        Assert.Contains("Lost connection", error.Message);
    }

    [Fact]
    public async Task CancellationByTheCallerIsNotReportedAsAnUnavailableShop()
    {
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        using var client = new OpenCartCatalogClient(UrlOf(silent), "key");
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UpsertCategoryAsync(Category, cancelled.Token));
    }
}
