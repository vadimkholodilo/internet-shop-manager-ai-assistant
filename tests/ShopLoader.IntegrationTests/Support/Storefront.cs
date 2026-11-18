using System.Net;

namespace ShopLoader.IntegrationTests;

/// <summary>The public storefront as a visitor sees it. This is the only place that knows its routes.</summary>
public sealed class Storefront : IDisposable
{
    private readonly HttpClient http;

    public Storefront(Uri shopUrl) => http = new HttpClient { BaseAddress = shopUrl, Timeout = TimeSpan.FromSeconds(60) };

    public Task<string> HomePageAsync() => http.GetStringAsync("index.php?route=common/home");

    public Task<string> ProductPageAsync(int productId) =>
        http.GetStringAsync($"index.php?route=product/product&language=ru-ru&product_id={productId}");

    /// <param name="path">The category ids from the top: one id for a top category, two for its child.</param>
    public Task<string> CategoryPageAsync(params int[] path) =>
        http.GetStringAsync($"index.php?route=product/category&language=ru-ru&path={string.Join('_', path)}");

    /// <summary>Any URL on the shop (a photo, a file path), without following it as a page. The caller disposes the response.</summary>
    public Task<HttpResponseMessage> GetAsync(string pathAndQuery) => http.GetAsync(pathAndQuery);

    public async Task<HttpStatusCode> StatusOfAsync(string pathAndQuery)
    {
        using var response = await http.GetAsync(pathAndQuery);
        return response.StatusCode;
    }

    public void Dispose() => http.Dispose();
}
