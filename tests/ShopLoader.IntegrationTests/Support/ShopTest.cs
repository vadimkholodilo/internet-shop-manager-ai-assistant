using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ShopClient;

namespace ShopLoader.IntegrationTests;

/// <summary>What a run of the loader printed and returned.</summary>
public sealed record LoaderRun(long ExitCode, string Output, string Error);

/// <summary>
/// Base class for integration tests: tests against the real shop, started once for all of them (see <see cref="ShopStack"/>).
/// It hands out the typed <see cref="CatalogApi"/> and <see cref="Storefront"/> and runs the loader, so a test reads as
/// what it checks and a change to the catalog contract is made in the helpers, not in every test.
/// Tests share one shop, so each one creates its own categories and products with the unique names from this class.
/// </summary>
[Collection("shop")]
public abstract class ShopTest(ShopStack stack)
{
    protected CatalogApi Api => stack.Api;

    protected Storefront Storefront => stack.Storefront;

    protected static string NewSlug() => "t-" + Unique();

    protected static string NewSku() => "T-" + Unique().ToUpperInvariant();

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    protected Task<ShopCategory> NewCategoryAsync() => Api.UpsertCategoryAsync(NewSlug(), "Для проверок");

    /// <summary>A product with a new SKU in a new category, unless a category is given.</summary>
    protected async Task<ShopProduct> NewProductAsync(string? categorySlug = null) =>
        await Api.UpsertProductAsync(NewSku(), new ProductSpec(categorySlug ?? (await NewCategoryAsync()).Slug));

    /// <summary>A catalog-loading client for the test shop, created the way any consumer does: through the factory.</summary>
    protected ICatalogLoadingClient NewClient() =>
        ShopClientFactory.CreateCatalogLoadingClient(ShopType.OpenCart, new ShopConnection(stack.ShopUrl, ShopStack.ApiKey));

    /// <summary>The test project's own photo, for tests that upload one (independent of the demo catalog).</summary>
    protected static string SampleImage => TestPhoto.Path;

    protected static TestCatalog NewCatalog() => new();

    /// <summary>Runs the loader in this process against the test shop (or another address / key, to test failures).</summary>
    protected async Task<LoaderRun> RunLoaderAsync(TestCatalog catalog, string? apiKey = null, string? url = null, int waitSeconds = 60)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await Cli.RunAsync(
            ["--file", catalog.File, "--images-dir", catalog.Images, "--url", url ?? stack.ShopUrl.ToString(),
             "--api-key", apiKey ?? ShopStack.ApiKey, "--wait-seconds", waitSeconds.ToString()],
            output, error);
        return new LoaderRun(exit, output.ToString(), error.ToString());
    }

    /// <summary>Runs the Native AOT loader image as a container against the test shop, on the sample catalog in data/.</summary>
    protected async Task<LoaderRun> RunSampleLoaderContainerAsync()
    {
        await using var loader = new ContainerBuilder(stack.LoaderImage)
            .WithNetwork(stack.Network)
            .WithEnvironment("SHOP_API_KEY", ShopStack.ApiKey)
            .WithCommand("--file", "/data/sample-catalog.json", "--images-dir", "/data/images", "--url", "http://shop")
            .Build();

        await loader.StartAsync();

        for (var i = 0; i < 100 && loader.State != TestcontainersStates.Exited; i++)
        {
            await Task.Delay(100);
        }

        var (stdout, stderr) = await loader.GetLogsAsync();
        return new LoaderRun(await loader.GetExitCodeAsync(), stdout, stderr);
    }
}
