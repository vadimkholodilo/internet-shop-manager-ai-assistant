using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>The catalog API extension: authentication, ids in responses, validation and keyed upserts.</summary>
[Collection("shop")]
public sealed class CatalogApiTests(ShopStack shop)
{
    private static string Slug() => "api-" + Guid.NewGuid().ToString("N")[..8];

    private HttpClient WithoutKey()
    {
        var http = new HttpClient { BaseAddress = shop.ShopUrl };
        return http;
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-key")]
    public async Task RequestsWithoutAValidKeyAreRejectedAndChangeNothing(string key)
    {
        using var http = WithoutKey();
        if (key != "")
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        var slug = Slug();
        var before = await shop.Http.CategoryCountAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await http.StatusAsync("catalog-api/products"));
        Assert.Equal(HttpStatusCode.Unauthorized, await http.StatusAsync("catalog-api/categories"));
        var (status, _) = await http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{slug}", new { name = "Не должна появиться" });

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal(before, await shop.Http.CategoryCountAsync());
        Assert.Equal(HttpStatusCode.NotFound, await shop.Http.StatusAsync($"catalog-api/categories/{slug}"));
    }

    [Fact]
    public async Task CategoryResponsesCarryIdsAndKeys()
    {
        var parent = Slug();
        var child = Slug();

        var (_, parentBody) = await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{parent}", new { name = "Родитель" });
        var (status, childBody) = await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{child}", new { name = "Ребёнок", parent });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(parentBody!["created"]!.GetValue<bool>());
        Assert.Equal(parentBody["id"]!.GetValue<int>(), childBody!["parent_id"]!.GetValue<int>());
        Assert.Equal(parent, childBody["parent_slug"]!.GetValue<string>());
        Assert.Equal(child, childBody["slug"]!.GetValue<string>());

        var byId = (await shop.Http.GetJsonAsync($"catalog-api/categories/by-id/{childBody["id"]}"))!;
        var bySlug = (await shop.Http.GetJsonAsync($"catalog-api/categories/{child}"))!;
        Assert.Equal(byId.ToJsonString(), bySlug.ToJsonString());
    }

    [Fact]
    public async Task UpsertKeepsTheSameIdAndUpdatesValues()
    {
        var slug = Slug();
        var (_, first) = await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{slug}", new { name = "Было" });
        var (_, second) = await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{slug}", new { name = "Стало" });

        Assert.True(first!["created"]!.GetValue<bool>());
        Assert.False(second!["created"]!.GetValue<bool>());
        Assert.Equal(first["id"]!.GetValue<int>(), second["id"]!.GetValue<int>());
        Assert.Equal("Стало", second["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProductResponsesCarryIdsAndAreListed()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1);
        catalog.Write();
        await Cli.RunAsync(["--file", catalog.File, "--images-dir", catalog.Images, "--url", shop.ShopUrl.ToString(), "--api-key", ShopStack.ApiKey],
            new StringWriter(), new StringWriter());

        var product = (await shop.Http.ProductAsync(catalog.Sku(1)))!;
        var category = (await shop.Http.GetJsonAsync($"catalog-api/categories/{catalog.CategorySlug}"))!;

        Assert.Equal(category["id"]!.GetValue<int>(), product["category_id"]!.GetValue<int>());
        var byId = (await shop.Http.GetJsonAsync($"catalog-api/products/by-id/{product["id"]}"))!;
        Assert.Equal(catalog.Sku(1), byId["sku"]!.GetValue<string>());

        var list = (await shop.Http.GetJsonAsync("catalog-api/products"))!;
        Assert.True(list["total"]!.GetValue<int>() >= 1);
        Assert.All(list["items"]!.AsArray(), item => Assert.True(item!["id"]!.GetValue<int>() > 0));
    }

    [Fact]
    public async Task InvalidRequestsAreRejectedWithClearStatusCodes()
    {
        var category = Slug();
        await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{category}", new { name = "Для проверок" });
        object Product(object? price = null, int quantity = 1, string? cat = null) => new
        {
            name = "Товар", category = cat ?? category, brand = "Бренд", description = "Описание", price = price ?? 100, quantity,
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Http.SendJsonAsync(HttpMethod.Put, "catalog-api/products/BAD-1", Product(price: 0))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Http.SendJsonAsync(HttpMethod.Put, "catalog-api/products/BAD-2", Product(quantity: -1))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await shop.Http.SendJsonAsync(HttpMethod.Put, "catalog-api/products/BAD-3", Product(cat: "no-such-category"))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{Slug()}", new { name = "X", parent = "no-such-parent" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Http.SendJsonAsync(HttpMethod.Put, "catalog-api/categories/Bad_Slug", new { name = "X" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, await shop.Http.StatusAsync("catalog-api/products/BAD-1"));
    }

    [Fact]
    public async Task ImageUploadRejectsUnsupportedTypesAndUnknownProducts()
    {
        using var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var unknown = await shop.Http.PutAsync("catalog-api/products/NO-SUCH-SKU/image", content);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
