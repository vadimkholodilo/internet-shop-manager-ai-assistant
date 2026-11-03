using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>User Story 2: the loader fills a real OpenCart shop through the catalog API.</summary>
[Collection("shop")]
public sealed class LoaderTests(ShopStack shop)
{
    private async Task<(int ExitCode, string Output, string Error)> RunAsync(TestCatalog catalog, string? apiKey = null, string? url = null, int wait = 60)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await Cli.RunAsync(
            ["--file", catalog.File, "--images-dir", catalog.Images, "--url", url ?? shop.ShopUrl.ToString(),
             "--api-key", apiKey ?? ShopStack.ApiKey, "--wait-seconds", wait.ToString()],
            output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task FirstLoadCreatesCategoriesProductsAndPhotos()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1, price: 12990m, quantity: 7).AddProduct(2);
        catalog.Write();

        var (exit, output, _) = await RunAsync(catalog);

        Assert.Equal(0, exit);
        Assert.Contains("Products: 2 created, 0 updated", output);
        var product = (await shop.Http.ProductAsync(catalog.Sku(1)))!;
        Assert.Equal(12990m, product["price"]!.GetValue<decimal>());
        Assert.Equal(7, product["quantity"]!.GetValue<int>());
        Assert.Equal($"Бренд {catalog.Prefix}", product["brand"]!.GetValue<string>());
        Assert.Equal(catalog.CategorySlug, product["category_slug"]!.GetValue<string>());
        Assert.Equal("Чёрный", product["attributes"]!["Цвет"]!.GetValue<string>());
        Assert.NotNull(product["image_url"]);

        using var photo = await shop.Http.GetAsync(new Uri(product["image_url"]!.GetValue<string>()).PathAndQuery);
        Assert.True(photo.IsSuccessStatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task SecondLoadCreatesNoDuplicates()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1).AddProduct(2).AddProduct(3);
        catalog.Write();
        await RunAsync(catalog);
        var products = await shop.Http.ProductCountAsync();
        var categories = await shop.Http.CategoryCountAsync();
        var firstId = (await shop.Http.ProductAsync(catalog.Sku(1)))!["id"]!.GetValue<int>();

        var (exit, output, _) = await RunAsync(catalog);

        Assert.Equal(0, exit);
        Assert.Contains("Products: 0 created, 3 updated", output);
        Assert.Equal(products, await shop.Http.ProductCountAsync());
        Assert.Equal(categories, await shop.Http.CategoryCountAsync());
        Assert.Equal(firstId, (await shop.Http.ProductAsync(catalog.Sku(1)))!["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task ChangedPriceIsAppliedAndOtherProductsStayTheSame()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1, price: 1000m).AddProduct(2, price: 2000m);
        catalog.Write();
        await RunAsync(catalog);

        catalog.SetPrice(1, 900m);
        catalog.Write();
        var (exit, _, _) = await RunAsync(catalog);

        Assert.Equal(0, exit);
        Assert.Equal(900m, (await shop.Http.ProductAsync(catalog.Sku(1)))!["price"]!.GetValue<decimal>());
        Assert.Equal(2000m, (await shop.Http.ProductAsync(catalog.Sku(2)))!["price"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task MalformedRecordIsReportedAndTheRestAreLoaded()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1);
        var bad = catalog.Product(2);
        bad["price"] = 0;
        catalog.AddRaw(bad).AddProduct(3);
        catalog.Write();

        var (exit, output, _) = await RunAsync(catalog);

        Assert.Equal(1, exit);
        Assert.Contains($"SKIPPED product {catalog.Sku(2)}", output);
        Assert.Contains("Failed records: 1", output);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, await shop.Http.StatusAsync($"catalog-api/products/{catalog.Sku(2)}"));
        Assert.NotNull(await shop.Http.ProductAsync(catalog.Sku(1)));
        Assert.NotNull(await shop.Http.ProductAsync(catalog.Sku(3)));
    }

    [Fact]
    public async Task CyrillicSpecialCharactersAndVeryLongValuesAreStoredCorrectly()
    {
        var longDescription = string.Concat(Enumerable.Repeat("Очень длинное описание товара. ", 200));
        using var catalog = new TestCatalog(shop.RepositoryRoot)
            .AddProduct(1, name: "Наушники «Ёлка» & <Pro> 5\" \"Max\"", description: longDescription,
                attributes: new JsonObject { ["Диагональ экрана"] = "6.1\"", ["Особенности"] = "Защита IP68; Wi-Fi 6 / BT 5.3" });
        catalog.Write();

        var (exit, _, _) = await RunAsync(catalog);

        Assert.Equal(0, exit);
        var product = (await shop.Http.ProductAsync(catalog.Sku(1)))!;
        Assert.Equal("Наушники «Ёлка» & <Pro> 5\" \"Max\"", product["name"]!.GetValue<string>());
        Assert.Equal(longDescription.Trim(), product["description"]!.GetValue<string>());
        Assert.Equal("6.1\"", product["attributes"]!["Диагональ экрана"]!.GetValue<string>());
        Assert.Equal("Защита IP68; Wi-Fi 6 / BT 5.3", product["attributes"]!["Особенности"]!.GetValue<string>());
    }

    [Fact]
    public async Task OutOfStockProductKeepsZeroQuantity()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1, quantity: 0);
        catalog.Write();

        await RunAsync(catalog);

        Assert.Equal(0, (await shop.Http.ProductAsync(catalog.Sku(1)))!["quantity"]!.GetValue<int>());
    }

    [Fact]
    public async Task InvalidFileFailsBeforeChangingAnything()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot);
        File.WriteAllText(catalog.File, "{ this is not json");
        var products = await shop.Http.ProductCountAsync();
        var categories = await shop.Http.CategoryCountAsync();

        var (exit, _, error) = await RunAsync(catalog);

        Assert.Equal(2, exit);
        Assert.Contains("not valid JSON", error);
        Assert.Equal(products, await shop.Http.ProductCountAsync());
        Assert.Equal(categories, await shop.Http.CategoryCountAsync());
    }

    [Fact]
    public async Task WrongApiKeyIsRejectedWithAClearMessage()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1);
        catalog.Write();

        var (exit, _, error) = await RunAsync(catalog, apiKey: "wrong-key");

        Assert.Equal(2, exit);
        Assert.Contains("401", error);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, await shop.Http.StatusAsync($"catalog-api/products/{catalog.Sku(1)}"));
    }

    [Fact]
    public async Task UnreachableShopFailsClearlyAfterTheWaitTime()
    {
        using var catalog = new TestCatalog(shop.RepositoryRoot).AddProduct(1);
        catalog.Write();

        var (exit, _, error) = await RunAsync(catalog, url: "http://127.0.0.1:9/", wait: 1);

        Assert.Equal(2, exit);
        Assert.Contains("did not become ready", error);
    }
}
