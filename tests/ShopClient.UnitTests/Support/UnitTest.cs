using System.Net;
using System.Net.Sockets;

namespace ShopClient.UnitTests;

/// <summary>
/// Base class for the library's unit tests. Everything that knows how a raw HTTP answer is written or how a test port is
/// opened lives here, so a test reads as "the shop did X, the client did Y". Unit tests cover logic that needs no shop;
/// what the real shop does belongs in the integration tests.
/// </summary>
public abstract class UnitTest
{
    protected static readonly CatalogCategory Category = new("c", "Категория", null);

    /// <summary>The address of a port nothing listens on.</summary>
    protected static Uri ClosedPortUrl()
    {
        using var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        return UrlOf(closed);
    }

    internal static Uri UrlOf(TcpListener listener) => new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");

    /// <summary>A raw HTTP response. <paramref name="headers"/> are complete header lines, each ending in a line break.</summary>
    protected static string Response(int code, string body = "", string contentType = "application/json", string headers = "") =>
        $"HTTP/1.1 {code} X\r\n{headers}Content-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";

    /// <summary>A connection to a scripted shop that waits only milliseconds between attempts.</summary>
    protected static ShopConnection Quick(ScriptedShop shop, int maxRetries = 3) =>
        new(shop.Url, "key") { MaxRetries = maxRetries, RetryDelay = TimeSpan.FromMilliseconds(10) };
}
