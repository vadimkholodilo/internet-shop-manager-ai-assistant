# Tests

Three projects, split by one question: **does the test need the shop?**

| Project | Put a test here when | How it runs |
|---------|----------------------|-------------|
| `ShopLoader.UnitTests` | it checks loader logic that needs no shop: validating a product file, parsing the command line, formatting the report | in milliseconds, no Docker |
| `ShopClient.UnitTests` | it checks the shared client library without a shop: choosing a client by shop type, how the client reports an unreachable shop | in milliseconds, no Docker |
| `ShopLoader.IntegrationTests` | it involves the shop: what the catalog API returns, what the storefront shows, what the loader or the shared client does to a shop (`ShopClientTests` uses the library alone, through its factory) | against a real OpenCart and MariaDB started with Testcontainers (Docker required) |

Nothing is mocked. (One accepted exception: `ShopClient.UnitTests` scripts HTTP answers such as 503 or `Retry-After` over a real socket to test the client's retry and error mapping, because the real shop cannot be made to answer that way on demand.) A scenario that cannot be reproduced against the real shop is not tested with a stand-in; it is
either made reproducible (two SKUs that the shop rejects with `409`) or left untested on purpose.

## Writing a unit test

Derive from `UnitTest` (`ShopLoader.UnitTests/Support`). It gives you a temporary workspace and everything that knows
the product-file format or the command-line options:

```csharp
public sealed class MyTests : UnitTest
{
    [Fact]
    public void PriceMustBePositive()
    {
        var result = Validate(Product(price: "0"));          // builds the file, runs the validator

        Assert.Contains("price", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public async Task MissingFileExitsWithTwo()
    {
        var run = await LoadWithoutAShopAsync(Path.Combine(Workspace.Path, "none.json"));

        Assert.Equal(2, run.Exit);
    }
}
```

## Writing an integration test

Derive from `ShopTest` (`ShopLoader.IntegrationTests/Support`). It gives you the typed `Api`, the `Storefront`, unique
names and a loader runner. All tests share one shop, so create your own data with `NewCategoryAsync()`,
`NewProductAsync()`, `NewSlug()` and `NewSku()`:

```csharp
public sealed class MyTests(ShopStack shop) : ShopTest(shop)
{
    [Fact]
    public async Task ProductShowsOnItsPage()
    {
        var product = await NewProductAsync();

        var page = await Storefront.ProductPageAsync(product.Id);

        Assert.Contains(product.Name, page);
    }

    [Fact]
    public async Task LoaderLoadsAFile()
    {
        using var catalog = NewCatalog().AddProduct(1, price: 990m);
        catalog.Write();

        var run = await RunLoaderAsync(catalog);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(990m, (await Api.GetProductAsync(catalog.Sku(1))).Price);
    }
}
```

`Api.TryUpsertProductAsync(...)` and the other `Try` methods return the status instead of failing, for tests of error
cases.

## When a contract changes

Change the helper, not the tests:

| What changes | Edit |
|--------------|------|
| catalog API paths, request bodies or response fields | `Support/CatalogApi.cs` |
| storefront routes | `Support/Storefront.cs` |
| the product file format | `Support/TestCatalog.cs` (integration) and `Support/UnitTest.cs` (unit) |
| command-line options | `Support/UnitTest.cs` and `Support/ShopTest.cs` |
