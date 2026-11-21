namespace ShopLoader.IntegrationTests;

/// <summary>
/// User Stories 1 and 3: the Native AOT loader image loads the demo catalog from data/ into the real shop,
/// the shop shows it in Russian, and a second run changes nothing.
/// </summary>
public sealed class SampleDemoTests(ShopStack shop) : ShopTest(shop)
{
    [Fact]
    public async Task SampleCatalogLoadsWithTheNativeAotLoaderAndAReRunCreatesNothingNew()
    {
        var first = await RunSampleLoaderContainerAsync();

        Assert.Equal(0, first.ExitCode);
        Assert.Contains("Failed records: 0", first.Output);
        var products = await Api.ProductCountAsync();
        Assert.True(products >= SampleCatalog.ProductCount, $"expected at least {SampleCatalog.ProductCount} products, found {products}");

        var second = await RunSampleLoaderContainerAsync();

        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Products: 0 created", second.Output);
        Assert.Equal(products, await Api.ProductCountAsync());
    }

    [Fact]
    public async Task StorefrontShowsTheSampleCatalogInRussianWithRubles()
    {
        await RunSampleLoaderContainerAsync();

        var home = await Storefront.HomePageAsync();
        Assert.Contains("lang=\"ru\"", home);
        Assert.Contains(SampleCatalog.TopCategoryNames[0], home);

        var sample = SampleCatalog.Product;
        var category = await Api.GetCategoryAsync(sample.Category);
        var categoryPage = await Storefront.CategoryPageAsync(category.ParentId!.Value, category.Id);
        Assert.Contains(category.Name, categoryPage);
        Assert.Contains("Сортировать по", categoryPage);

        var details = await Storefront.ProductPageAsync((await Api.GetProductAsync(sample.Sku)).Id);

        Assert.Contains(sample.Name, details);
        Assert.Contains(sample.Price, details);
        Assert.Contains("Бренд:", details);
        Assert.Contains(sample.Brand, details);
        Assert.Contains(sample.Attribute, details);
        Assert.Contains("В наличии", details);
        Assert.Contains($"/image/cache/catalog/products/{sample.Image}", details);
    }
}
