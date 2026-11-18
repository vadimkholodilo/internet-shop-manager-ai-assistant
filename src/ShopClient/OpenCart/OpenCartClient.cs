using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ShopClient.OpenCart;

/// <summary>
/// The only class that knows about OpenCart. It talks to the catalog API extension
/// (specs/001-test-shop/contracts/catalog-endpoint.md) and never to the shop's database.
/// </summary>
internal sealed class OpenCartClient : ICatalogLoadingClient
{
    private readonly HttpClient http;
    private readonly TimeSpan requestTimeout;

    public OpenCartClient(ShopConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        requestTimeout = connection.RequestTimeout;
        http = new HttpClient
        {
            BaseAddress = new Uri(connection.Url.ToString().TrimEnd('/') + "/"),
            Timeout = requestTimeout,
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
    }

    public async Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        var clock = Stopwatch.StartNew();
        string lastProblem = "not tried yet";

        for (var attemptNumber = 0; ; attemptNumber++)
        {
            // A zero timeout still gets one attempt.
            var remaining = timeout - clock.Elapsed;
            if (attemptNumber > 0 && remaining <= TimeSpan.Zero)
            {
                throw new ShopUnavailableException(
                    $"The shop did not become ready at {http.BaseAddress} within {timeout.TotalSeconds:0.##}s ({lastProblem}).");
            }

            // One attempt never outlives the overall timeout, even when the shop accepts the connection and then hangs.
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attempt.CancelAfter(remaining > TimeSpan.Zero && remaining < requestTimeout ? remaining : requestTimeout);
            try
            {
                using var response = await http.GetAsync("catalog-api/categories", attempt.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ShopUnavailableException(
                        "The shop rejected the API key (401). Check SHOP_API_KEY / --api-key.", response.StatusCode);
                }
                lastProblem = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException e)
            {
                lastProblem = e.Message;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastProblem = "request timed out";
            }

            var pause = TimeSpan.FromSeconds(3);
            var left = timeout - clock.Elapsed;
            await Task.Delay(left < pause ? (left > TimeSpan.Zero ? left : TimeSpan.Zero) : pause, cancellationToken);
        }
    }

    public async Task<UpsertResult> UpsertCategoryAsync(CatalogCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentChecks.Category(category);

        return await PutJsonAsync($"catalog-api/categories/{Uri.EscapeDataString(category.Slug)}",
            new CategoryRequest(category.Name, category.Parent), OpenCartJsonContext.Default.CategoryRequest, cancellationToken);
    }

    public async Task<UpsertResult> UpsertProductAsync(CatalogProduct product, CancellationToken cancellationToken = default)
    {
        ArgumentChecks.Product(product);

        return await PutJsonAsync($"catalog-api/products/{Uri.EscapeDataString(product.Sku)}",
            new ProductRequest(product.Name, product.Category, product.Brand, product.Description,
                product.Attributes, product.Price, product.Quantity),
            OpenCartJsonContext.Default.ProductRequest, cancellationToken);
    }

    public async Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var contentType = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            var other => throw new CatalogException($"unsupported image type: {other}"),
        };

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A missing or unreadable file belongs to this one product, like a rejected image does.
            throw new CatalogException($"cannot read the image {imagePath}: {e.Message}", innerException: e);
        }

        using var content = new ByteArrayContent(bytes);
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

        ItemResponse? item;
        try
        {
            item = await response.Content.ReadFromJsonAsync(OpenCartJsonContext.Default.ItemResponse, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            // NotSupportedException: the answer is not JSON at all, for example an HTML page from a proxy.
            throw new CatalogException("the shop returned a response that is not valid JSON", response.StatusCode, e);
        }

        return item is null
            ? throw new CatalogException("the shop returned an empty response", response.StatusCode)
            : new UpsertResult(item.Id, item.Created ?? false);
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
            throw new ShopUnavailableException($"Lost connection to the shop: {e.Message}", e.StatusCode, e);
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout this way. A shop that stops answering ends the run like a lost
            // connection does, instead of making every remaining record wait for the timeout.
            throw new ShopUnavailableException($"The shop did not answer within {requestTimeout.TotalSeconds:0.##} seconds.", innerException: e);
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
            throw new ShopUnavailableException("The shop rejected the API key (401).", response.StatusCode);
        }
        if (response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
        {
            // The shop, or the proxy in front of it, says it cannot serve anyone right now; this is not about the record.
            throw new ShopUnavailableException($"The shop is not available (HTTP {(int)response.StatusCode}).", response.StatusCode);
        }

        var message = $"HTTP {(int)response.StatusCode}";
        try
        {
            var error = await response.Content.ReadFromJsonAsync(OpenCartJsonContext.Default.ErrorResponse, cancellationToken);
            if (!string.IsNullOrEmpty(error?.Error))
            {
                message += $": {Truncate(error.Error)}";
            }
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            // Not a JSON error body; the status code is enough.
        }

        throw new CatalogException(message, response.StatusCode);
    }

    private static string Truncate(string text) => text.Length <= 500 ? text : text[..500] + "…";

    public void Dispose() => http.Dispose();
}
