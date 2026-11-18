namespace ShopLoader.IntegrationTests;

/// <summary>
/// User Stories 1 and 3: the Native AOT loader image loads the generated sample catalog into the real shop,
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
        Assert.True(products >= 20, $"expected at least 20 products, found {products}");

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
        Assert.Contains("Электроника", home);

        var phones = await Api.GetCategoryAsync("smartphones");
        var category = await Storefront.CategoryPageAsync(phones.ParentId!.Value, phones.Id);

        Assert.Contains("Смартфон Norda Pulse 5 128 ГБ", category);
        Assert.Contains("24 990 ₽", category);
        Assert.Contains("Сортировать по", category);
        Assert.Contains("/image/cache/catalog/products/ph-001", category);

        var details = await Storefront.ProductPageAsync((await Api.GetProductAsync("PH-001")).Id);
        Assert.Contains("Бренд:", details);
        Assert.Contains("Norda", details);
        Assert.Contains("Диагональ экрана", details);
        Assert.Contains("В наличии", details);
    }
}
