using System.Net;

namespace ShopLoader.IntegrationTests;

/// <summary>What the public storefront serves: catalog text must not become markup, and internal files must stay private.</summary>
[Collection("shop")]
public sealed class StorefrontSecurityTests(ShopStack shop)
{
    private const string Script = "<script>alert(1)</script>";

    [Fact]
    public async Task TextFromTheCatalogIsEscapedOnTheStorefrontAndRoundTripsThroughTheApi()
    {
        // OpenCart prints stored text as HTML, so catalog data (which may come from a third party) must be stored encoded.
        var slug = "xss-" + Guid.NewGuid().ToString("N")[..8];
        var sku = "XSS-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var (_, category) = await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/categories/{slug}", new { name = "Категория " + Script });
        await shop.Http.SendJsonAsync(HttpMethod.Put, $"catalog-api/products/{sku}", new
        {
            name = "Товар " + Script,
            category = slug,
            brand = "Бренд <b>x</b>",
            description = "Описание " + Script,
            attributes = new Dictionary<string, string> { ["Свойство <i>k</i>"] = "Значение <u>v</u>" },
            price = 100,
            quantity = 1,
        });
        var product = (await shop.Http.ProductAsync(sku))!;

        using var storefront = new HttpClient { BaseAddress = shop.ShopUrl };
        var productPage = await storefront.GetStringAsync($"index.php?route=product/product&language=ru-ru&product_id={product["id"]}");
        var categoryPage = await storefront.GetStringAsync($"index.php?route=product/category&language=ru-ru&path={category!["id"]}");

        foreach (var page in new[] { productPage, categoryPage })
        {
            Assert.DoesNotContain(Script, page);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", page);
        }
        Assert.DoesNotContain("<b>x</b>", productPage);
        Assert.DoesNotContain("<i>k</i>", productPage);
        Assert.DoesNotContain("<u>v</u>", productPage);

        Assert.Equal("Товар " + Script, product["name"]!.GetValue<string>());
        Assert.Equal("Бренд <b>x</b>", product["brand"]!.GetValue<string>());
        Assert.Equal("Описание " + Script, product["description"]!.GetValue<string>());
        Assert.Equal("Значение <u>v</u>", product["attributes"]!["Свойство <i>k</i>"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("system/storage/logs/error.log")]
    [InlineData("system/storage/session/")]
    public async Task OpenCartStorageIsNotServed(string path)
    {
        using var storefront = new HttpClient { BaseAddress = shop.ShopUrl };

        using var response = await storefront.GetAsync(path);

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"{path} -> {(int)response.StatusCode}");
    }
}
