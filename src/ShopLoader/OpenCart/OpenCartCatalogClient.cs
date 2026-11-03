using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ShopLoader.Json;
using ShopLoader.Models;

namespace ShopLoader.OpenCart;

/// <summary>
/// The only class that knows about OpenCart. It talks to the catalog API extension
/// (specs/001-test-shop/contracts/catalog-endpoint.md) and never to the shop's database.
/// </summary>
internal sealed class OpenCartCatalogClient : ICatalogClient, IDisposable
{
    private readonly HttpClient http;

    public OpenCartCatalogClient(Uri shopUrl, string apiKey)
    {
        http = new HttpClient { BaseAddress = new Uri(shopUrl.ToString().TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastProblem = "not tried yet";

        while (true)
        {
            try
            {
                using var response = await http.GetAsync("catalog-api/categories", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ShopUnavailableException("The shop rejected the API key (401). Check SHOP_API_KEY / --api-key.");
                }
                lastProblem = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException e)
            {
                lastProblem = e.Message;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastProblem = "request timed out";
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new ShopUnavailableException(
                    $"The shop did not become ready at {http.BaseAddress} within {timeout.TotalSeconds:0}s ({lastProblem}).");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    public Task<UpsertResult> UpsertCategoryAsync(CategoryRecord category, CancellationToken cancellationToken) =>
        PutJsonAsync($"catalog-api/categories/{Uri.EscapeDataString(category.Slug)}",
            new CategoryRequest(category.Name, category.Parent), LoaderJsonContext.Default.CategoryRequest, cancellationToken);

    public Task<UpsertResult> UpsertProductAsync(ProductRecord product, CancellationToken cancellationToken) =>
        PutJsonAsync($"catalog-api/products/{Uri.EscapeDataString(product.Sku)}",
            new ProductRequest(product.Name, product.Category, product.Brand, product.Description,
                product.Attributes, product.Price, product.Quantity),
            LoaderJsonContext.Default.ProductRequest, cancellationToken);

    public async Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken)
    {
        var contentType = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            var other => throw new CatalogException($"unsupported image type: {other}"),
        };

        using var content = new ByteArrayContent(await File.ReadAllBytesAsync(imagePath, cancellationToken));
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await SendAsync(HttpMethod.Put, $"catalog-api/products/{Uri.EscapeDataString(sku)}/image", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<UpsertResult> PutJsonAsync<T>(
        string path, T body, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(body, typeInfo);
        using var response = await SendAsync(HttpMethod.Put, path, content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var item = await response.Content.ReadFromJsonAsync(LoaderJsonContext.Default.ItemResponse, cancellationToken)
                   ?? throw new CatalogException("the shop returned an empty response");
        return new UpsertResult(item.Id, item.Created ?? false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path) { Content = content };
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new ShopUnavailableException($"Lost connection to the shop: {e.Message}");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ShopUnavailableException("The shop rejected the API key (401).");
        }

        var message = $"HTTP {(int)response.StatusCode}";
        try
        {
            var error = await response.Content.ReadFromJsonAsync(LoaderJsonContext.Default.ErrorResponse, cancellationToken);
            if (!string.IsNullOrEmpty(error?.Error))
            {
                message += $": {error.Error}";
            }
        }
        catch (JsonException)
        {
            // Not a JSON error body; the status code is enough.
        }

        throw new CatalogException(message);
    }

    public void Dispose() => http.Dispose();
}
