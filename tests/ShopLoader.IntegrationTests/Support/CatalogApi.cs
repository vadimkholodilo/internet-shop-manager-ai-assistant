using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>A category as the catalog API returns it.</summary>
public sealed record ShopCategory(int Id, string Slug, string Name, int? ParentId, string? ParentSlug, bool? Created);

/// <summary>A product as the catalog API returns it.</summary>
public sealed record ShopProduct(
    int Id, string Sku, string Name, int CategoryId, string? CategorySlug, string Brand, string Description,
    IReadOnlyDictionary<string, string> Attributes, decimal Price, int Quantity, string? ImageUrl, bool? Created);

/// <summary>What a test sends to create or update a product. Only the category is required; the rest has plausible defaults.</summary>
public sealed record ProductSpec(
    string Category, string Name = "Товар", string Brand = "Бренд", string Description = "Описание",
    decimal Price = 100m, int Quantity = 1, IReadOnlyDictionary<string, string>? Attributes = null);

public sealed record ShopPage<T>(int Total, IReadOnlyList<T> Items);

/// <summary>The status of a call and, when it succeeded, what it returned. <see cref="Body"/> is kept for failure messages.</summary>
public sealed record ApiResult<T>(HttpStatusCode Status, T? Value, string Body)
{
    public bool Succeeded => Status == HttpStatusCode.OK;
}

/// <summary>
/// The catalog API of the shop (specs/001-test-shop/contracts/catalog-endpoint.md) as typed calls. This is the only
/// place that knows its paths, request bodies and response fields, so when the contract changes, only this file does.
/// Methods starting with Try return the status for tests of error cases; the others fail the test unless the call succeeds.
/// </summary>
public sealed class CatalogApi : IDisposable
{
    private const string Categories = "catalog-api/categories";
    private const string Products = "catalog-api/products";

    private readonly Uri shopUrl;
    private readonly HttpClient http;

    /// <param name="apiKey">Sent as the bearer token; null or empty sends none.</param>
    public CatalogApi(Uri shopUrl, string? apiKey)
    {
        this.shopUrl = shopUrl;
        http = new HttpClient { BaseAddress = shopUrl, Timeout = TimeSpan.FromSeconds(60) };
        if (!string.IsNullOrEmpty(apiKey))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    /// <summary>The same API with another key (null or empty: no key), for authentication tests. The caller disposes it.</summary>
    public CatalogApi WithKey(string? apiKey) => new(shopUrl, apiKey);

    // ------------------------------------------------------------------------------------ categories

    public Task<ApiResult<ShopCategory>> TryUpsertCategoryAsync(string slug, string name, string? parent = null) =>
        SendAsync(HttpMethod.Put, $"{Categories}/{Escape(slug)}", new { name, parent }, ToCategory);

    public async Task<ShopCategory> UpsertCategoryAsync(string slug, string name, string? parent = null) =>
        Succeeded(await TryUpsertCategoryAsync(slug, name, parent));

    public Task<ApiResult<ShopCategory>> TryGetCategoryAsync(string slug) =>
        SendAsync(HttpMethod.Get, $"{Categories}/{Escape(slug)}", null, ToCategory);

    public async Task<ShopCategory> GetCategoryAsync(string slug) => Succeeded(await TryGetCategoryAsync(slug));

    public async Task<ShopCategory> GetCategoryByIdAsync(int id) =>
        Succeeded(await SendAsync(HttpMethod.Get, $"{Categories}/by-id/{id}", null, ToCategory));

    public Task<ApiResult<ShopPage<ShopCategory>>> TryListCategoriesAsync(int page = 1) =>
        SendAsync(HttpMethod.Get, $"{Categories}?page={page}", null, node => ToPage(node, ToCategory));

    public async Task<int> CategoryCountAsync() => Succeeded(await TryListCategoriesAsync()).Total;

    // -------------------------------------------------------------------------------------- products

    public Task<ApiResult<ShopProduct>> TryUpsertProductAsync(string sku, ProductSpec spec) =>
        SendAsync(HttpMethod.Put, $"{Products}/{Escape(sku)}", new
        {
            name = spec.Name,
            category = spec.Category,
            brand = spec.Brand,
            description = spec.Description,
            attributes = spec.Attributes ?? new Dictionary<string, string>(),
            price = spec.Price,
            quantity = spec.Quantity,
        }, ToProduct);

    public async Task<ShopProduct> UpsertProductAsync(string sku, ProductSpec spec) =>
        Succeeded(await TryUpsertProductAsync(sku, spec));

    public Task<ApiResult<ShopProduct>> TryGetProductAsync(string sku) =>
        SendAsync(HttpMethod.Get, $"{Products}/{Escape(sku)}", null, ToProduct);

    public async Task<ShopProduct> GetProductAsync(string sku) => Succeeded(await TryGetProductAsync(sku));

    public async Task<ShopProduct> GetProductByIdAsync(int id) =>
        Succeeded(await SendAsync(HttpMethod.Get, $"{Products}/by-id/{id}", null, ToProduct));

    public Task<ApiResult<ShopPage<ShopProduct>>> TryListProductsAsync(int page = 1) =>
        SendAsync(HttpMethod.Get, $"{Products}?page={page}", null, node => ToPage(node, ToProduct));

    public async Task<ShopPage<ShopProduct>> ListProductsAsync(int page = 1) => Succeeded(await TryListProductsAsync(page));

    public async Task<int> ProductCountAsync() => (await ListProductsAsync()).Total;

    public async Task<HttpStatusCode> UploadImageAsync(string sku, string contentType, byte[] bytes)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await http.PutAsync($"{Products}/{Escape(sku)}/image", content);
        return response.StatusCode;
    }

    // ------------------------------------------------------------------------------- representations

    private static ShopCategory ToCategory(JsonNode node) => new(
        node["id"]!.GetValue<int>(), node["slug"]!.GetValue<string>(), node["name"]!.GetValue<string>(),
        node["parent_id"]?.GetValue<int>(), node["parent_slug"]?.GetValue<string>(), node["created"]?.GetValue<bool>());

    private static ShopProduct ToProduct(JsonNode node) => new(
        node["id"]!.GetValue<int>(), node["sku"]!.GetValue<string>(), node["name"]!.GetValue<string>(),
        node["category_id"]!.GetValue<int>(), node["category_slug"]?.GetValue<string>(), node["brand"]!.GetValue<string>(),
        node["description"]!.GetValue<string>(),
        node["attributes"]!.AsObject().ToDictionary(a => a.Key, a => a.Value!.GetValue<string>()),
        node["price"]!.GetValue<decimal>(), node["quantity"]!.GetValue<int>(), node["image_url"]?.GetValue<string>(),
        node["created"]?.GetValue<bool>());

    private static ShopPage<T> ToPage<T>(JsonNode node, Func<JsonNode, T> item) =>
        new(node["total"]!.GetValue<int>(), [.. node["items"]!.AsArray().Select(i => item(i!))]);

    // ----------------------------------------------------------------------------------------- plumbing

    private static string Escape(string key) => Uri.EscapeDataString(key);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, Func<JsonNode, T> map)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var value = response.IsSuccessStatusCode ? map(JsonNode.Parse(text)!) : default;
        return new ApiResult<T>(response.StatusCode, value, text);
    }

    private static T Succeeded<T>(ApiResult<T> result)
    {
        Assert.True(result.Succeeded, $"the catalog API answered {(int)result.Status}: {result.Body}");
        return result.Value!;
    }

    public void Dispose() => http.Dispose();
}
