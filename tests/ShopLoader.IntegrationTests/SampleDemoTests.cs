using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace ShopLoader.IntegrationTests;

/// <summary>
/// User Stories 1 and 3: the Native AOT loader image loads the generated sample catalog into the real shop,
/// the shop shows it in Russian, and a second run changes nothing.
/// </summary>
[Collection("shop")]
public sealed class SampleDemoTests(ShopStack shop)
{
    private async Task<(long ExitCode, string Logs)> RunLoaderContainerAsync()
    {
        await using var loader = new ContainerBuilder(shop.LoaderImage)
            .WithNetwork(shop.Network)
            .WithEnvironment("SHOP_API_KEY", ShopStack.ApiKey)
            .WithCommand("--file", "/data/sample-catalog.json", "--images-dir", "/data/images", "--url", "http://shop")
            .Build();

        await loader.StartAsync();

        for (var i = 0; i < 100 && loader.State != TestcontainersStates.Exited; i++)
        {
            await Task.Delay(100);
        }

        var (stdout, stderr) = await loader.GetLogsAsync();
        return (await loader.GetExitCodeAsync(), stdout + stderr);
    }

    [Fact]
    public async Task SampleCatalogLoadsWithTheNativeAotLoaderAndAReRunCreatesNothingNew()
    {
        var first = await RunLoaderContainerAsync();

        Assert.Equal(0, first.ExitCode);
        Assert.Contains("Failed records: 0", first.Logs);
        var products = await shop.Http.ProductCountAsync();
        Assert.True(products >= 20, $"expected at least 20 products, found {products}");

        var second = await RunLoaderContainerAsync();

        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Products: 0 created", second.Logs);
        Assert.Equal(products, await shop.Http.ProductCountAsync());
    }

    [Fact]
    public async Task StorefrontShowsTheSampleCatalogInRussianWithRubles()
    {
        await RunLoaderContainerAsync();
        using var storefront = new HttpClient { BaseAddress = shop.ShopUrl };

        var home = await storefront.GetStringAsync("index.php?route=common/home");
        Assert.Contains("lang=\"ru\"", home);
        Assert.Contains("Электроника", home);

        var phones = (await shop.Http.GetJsonAsync("catalog-api/categories/smartphones"))!;
        var parent = phones["parent_id"]!.GetValue<int>();
        var page = await storefront.GetStringAsync($"index.php?route=product/category&language=ru-ru&path={parent}_{phones["id"]}");

        Assert.Contains("Смартфон Norda Pulse 5 128 ГБ", page);
        Assert.Contains("24 990 ₽", page);
        Assert.Contains("Сортировка", page);
        Assert.Contains("/image/cache/catalog/products/ph-001", page);

        var product = (await shop.Http.ProductAsync("PH-001"))!;
        var details = await storefront.GetStringAsync($"index.php?route=product/product&language=ru-ru&product_id={product["id"]}");
        Assert.Contains("Производитель", details);
        Assert.Contains("Norda", details);
        Assert.Contains("Диагональ экрана", details);
        Assert.Contains("В наличии", details);
    }

    [Fact]
    public async Task EveryLoadedSampleProductHasAPhotoAndAPrice()
    {
        await RunLoaderContainerAsync();

        var items = (await shop.Http.GetJsonAsync("catalog-api/products?page=1"))!["items"]!.AsArray()
            .Where(i => i!["sku"]!.GetValue<string>().Length == 6 && i["sku"]!.GetValue<string>()[2] == '-')
            .ToList();

        Assert.NotEmpty(items);
        Assert.All(items, i =>
        {
            Assert.True(i!["price"]!.GetValue<decimal>() > 0);
            Assert.NotNull(i["image_url"]);
        });
    }
}
