using System.Net;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>User Story 2: the loader fills a real OpenCart shop through the catalog API.</summary>
public sealed class LoaderTests(ShopStack shop) : ShopTest(shop)
{
    [Fact]
    public async Task FirstLoadCreatesCategoriesProductsAndPhotos()
    {
        using var catalog = NewCatalog().AddProduct(1, price: 12990m, quantity: 7).AddProduct(2);
        catalog.Write();

        var run = await RunLoaderAsync(catalog);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Products: 2 created, 0 updated", run.Output);
        var product = await Api.GetProductAsync(catalog.Sku(1));
        Assert.Equal(12990m, product.Price);
        Assert.Equal(7, product.Quantity);
        Assert.Equal($"Бренд {catalog.Prefix}", product.Brand);
        Assert.Equal(catalog.CategorySlug, product.CategorySlug);
        Assert.Equal("Чёрный", product.Attributes["Цвет"]);

        Assert.NotNull(product.ImageUrl);
        using var photo = await Storefront.GetAsync(new Uri(product.ImageUrl).PathAndQuery);
        Assert.True(photo.IsSuccessStatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task SecondLoadCreatesNoDuplicates()
    {
        using var catalog = NewCatalog().AddProduct(1).AddProduct(2).AddProduct(3);
        catalog.Write();
        await RunLoaderAsync(catalog);
        var products = await Api.ProductCountAsync();
        var categories = await Api.CategoryCountAsync();
        var firstId = (await Api.GetProductAsync(catalog.Sku(1))).Id;

        var run = await RunLoaderAsync(catalog);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Products: 0 created, 3 updated", run.Output);
        Assert.Equal(products, await Api.ProductCountAsync());
        Assert.Equal(categories, await Api.CategoryCountAsync());
        Assert.Equal(firstId, (await Api.GetProductAsync(catalog.Sku(1))).Id);
    }

    [Fact]
    public async Task ChangedPriceIsAppliedAndOtherProductsStayTheSame()
    {
        using var catalog = NewCatalog().AddProduct(1, price: 1000m).AddProduct(2, price: 2000m);
        catalog.Write();
        await RunLoaderAsync(catalog);

        catalog.SetPrice(1, 900m);
        catalog.Write();
        var run = await RunLoaderAsync(catalog);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(900m, (await Api.GetProductAsync(catalog.Sku(1))).Price);
        Assert.Equal(2000m, (await Api.GetProductAsync(catalog.Sku(2))).Price);
    }

    [Fact]
    public async Task MalformedRecordIsReportedAndTheRestAreLoaded()
    {
        using var catalog = NewCatalog().AddProduct(1);
        var bad = catalog.Product(2);
        bad["price"] = 0;
        catalog.AddRaw(bad).AddProduct(3);
        catalog.Write();

        var run = await RunLoaderAsync(catalog);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains($"SKIPPED product {catalog.Sku(2)}", run.Output);
        Assert.Contains("Failed records: 1", run.Output);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryGetProductAsync(catalog.Sku(2))).Status);
        await Api.GetProductAsync(catalog.Sku(1));
        await Api.GetProductAsync(catalog.Sku(3));
    }

    [Fact]
    public async Task ProductTheShopRejectsIsReportedAndTheRestAreLoaded()
    {
        // The validator accepts both SKUs, but the shop gives them the same URL keyword and answers 409 for the second.
        using var catalog = NewCatalog().AddProduct(1);
        var colliding = catalog.Product(2);
        var collidingSku = catalog.Sku(1).Replace('-', '_');
        colliding["sku"] = collidingSku;
        catalog.AddRaw(colliding).AddProduct(3);
        catalog.Write();

        var run = await RunLoaderAsync(catalog);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains($"FAILED product {collidingSku}", run.Output);
        Assert.Contains("409", run.Output);
        Assert.Contains("Products: 2 created", run.Output);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryGetProductAsync(collidingSku)).Status);
        await Api.GetProductAsync(catalog.Sku(1));
        await Api.GetProductAsync(catalog.Sku(3));
    }

    [Fact]
    public async Task CyrillicSpecialCharactersAndVeryLongValuesAreStoredCorrectly()
    {
        var longDescription = string.Concat(Enumerable.Repeat("Очень длинное описание товара. ", 200));
        using var catalog = NewCatalog()
            .AddProduct(1, name: "Наушники «Ёлка» & <Pro> 5\" \"Max\"", description: longDescription,
                attributes: new JsonObject { ["Диагональ экрана"] = "6.1\"", ["Особенности"] = "Защита IP68; Wi-Fi 6 / BT 5.3" });
        catalog.Write();

        var run = await RunLoaderAsync(catalog);

        Assert.Equal(0, run.ExitCode);
        var product = await Api.GetProductAsync(catalog.Sku(1));
        Assert.Equal("Наушники «Ёлка» & <Pro> 5\" \"Max\"", product.Name);
        Assert.Equal(longDescription.Trim(), product.Description);
        Assert.Equal("6.1\"", product.Attributes["Диагональ экрана"]);
        Assert.Equal("Защита IP68; Wi-Fi 6 / BT 5.3", product.Attributes["Особенности"]);
    }

    [Fact]
    public async Task OutOfStockProductKeepsZeroQuantity()
    {
        using var catalog = NewCatalog().AddProduct(1, quantity: 0);
        catalog.Write();

        await RunLoaderAsync(catalog);

        Assert.Equal(0, (await Api.GetProductAsync(catalog.Sku(1))).Quantity);
    }

    [Fact]
    public async Task WrongApiKeyIsRejectedWithAClearMessage()
    {
        using var catalog = NewCatalog().AddProduct(1);
        catalog.Write();

        var run = await RunLoaderAsync(catalog, apiKey: "wrong-key");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("401", run.Error);
        Assert.Contains("--api-key", run.Error);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryGetProductAsync(catalog.Sku(1))).Status);
    }
}
