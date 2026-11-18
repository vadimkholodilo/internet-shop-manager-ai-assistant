using System.Net;

namespace ShopLoader.IntegrationTests;

/// <summary>What the public storefront serves: catalog text must not become markup, and internal files must stay private.</summary>
public sealed class StorefrontSecurityTests(ShopStack shop) : ShopTest(shop)
{
    private const string Script = "<script>alert(1)</script>";

    [Fact]
    public async Task TextFromTheCatalogIsEscapedOnTheStorefrontAndRoundTripsThroughTheApi()
    {
        // OpenCart prints stored text as HTML, so catalog data (which may come from a third party) must be stored encoded.
        var category = await Api.UpsertCategoryAsync(NewSlug(), "Категория " + Script);
        var sent = new ProductSpec(
            category.Slug,
            Name: "Товар " + Script,
            Brand: "Бренд <b>x</b>",
            Description: "Описание " + Script,
            Attributes: new Dictionary<string, string> { ["Свойство <i>k</i>"] = "Значение <u>v</u>" });
        var product = await Api.UpsertProductAsync(NewSku(), sent);

        var productPage = await Storefront.ProductPageAsync(product.Id);
        var categoryPage = await Storefront.CategoryPageAsync(category.Id);

        foreach (var page in new[] { productPage, categoryPage })
        {
            Assert.DoesNotContain(Script, page);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", page);
        }
        Assert.DoesNotContain("<b>x</b>", productPage);
        Assert.DoesNotContain("<i>k</i>", productPage);
        Assert.DoesNotContain("<u>v</u>", productPage);

        Assert.Equal(sent.Name, product.Name);
        Assert.Equal(sent.Brand, product.Brand);
        Assert.Equal(sent.Description, product.Description);
        Assert.Equal("Значение <u>v</u>", product.Attributes["Свойство <i>k</i>"]);
    }

    [Theory]
    [InlineData("system/storage/logs/error.log")]
    [InlineData("system/storage/session/")]
    public async Task OpenCartStorageIsNotServed(string path)
    {
        var status = await Storefront.StatusOfAsync(path);

        Assert.True(status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"{path} -> {(int)status}");
    }
}
