using ShopLoader.Models;
using ShopLoader.Validation;

namespace ShopLoader.UnitTests;

public sealed class LoaderTests
{
    private sealed class FakeShop : ICatalogClient
    {
        public List<string> Calls { get; } = [];
        public HashSet<string> Failing { get; } = [];
        private readonly HashSet<string> known = [];

        public Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<UpsertResult> UpsertCategoryAsync(CategoryRecord category, CancellationToken cancellationToken) =>
            Upsert("category", category.Slug);

        public Task<UpsertResult> UpsertProductAsync(ProductRecord product, CancellationToken cancellationToken) =>
            Upsert("product", product.Sku);

        public Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken)
        {
            Calls.Add($"image {sku}");
            return Task.CompletedTask;
        }

        private Task<UpsertResult> Upsert(string kind, string key)
        {
            Calls.Add($"{kind} {key}");
            if (Failing.Contains(key))
            {
                throw new CatalogException("HTTP 400: rejected");
            }
            return Task.FromResult(new UpsertResult(Calls.Count, known.Add($"{kind} {key}")));
        }
    }

    private static ProductRecord Product(string sku, string category = "phones") =>
        new(sku, "Имя", category, "Acme", "Описание", new Dictionary<string, string>(), 100m, 1, "a.png");

    private static ValidationResult Validation(IEnumerable<CategoryRecord> categories, IEnumerable<ProductRecord> products, params RecordError[] errors) =>
        new(new ProductFile([.. categories], [.. products]), errors);

    private static readonly CategoryRecord Phones = new("phones", "Телефоны", null);

    [Fact]
    public async Task LoadsCategoriesThenProductsWithImages()
    {
        var shop = new FakeShop();
        var report = await new Loader(shop, new StringWriter()).LoadAsync(
            Validation([Phones], [Product("A")]), "images", CancellationToken.None);

        Assert.Equal(["category phones", "product A", "image A"], shop.Calls);
        Assert.True(report.Succeeded);
        Assert.Equal(1, report.CategoriesCreated);
        Assert.Equal(1, report.ProductsCreated);
    }

    [Fact]
    public async Task SecondRunCountsUpdatesNotCreations()
    {
        var shop = new FakeShop();
        var loader = new Loader(shop, new StringWriter());
        var validation = Validation([Phones], [Product("A")]);

        await loader.LoadAsync(validation, "images", CancellationToken.None);
        var second = await loader.LoadAsync(validation, "images", CancellationToken.None);

        Assert.Equal(0, second.ProductsCreated);
        Assert.Equal(1, second.ProductsUpdated);
        Assert.Equal(1, second.CategoriesUpdated);
    }

    [Fact]
    public async Task FailedProductIsReportedAndTheRestContinue()
    {
        var shop = new FakeShop { Failing = { "BAD" } };
        var output = new StringWriter();
        var report = await new Loader(shop, output).LoadAsync(
            Validation([Phones], [Product("BAD"), Product("GOOD")]), "images", CancellationToken.None);

        Assert.False(report.Succeeded);
        var failure = Assert.Single(report.Failures);
        Assert.Equal("BAD", failure.Key);
        Assert.Equal(1, report.ProductsCreated);
        Assert.Contains("FAILED product BAD", output.ToString());
        Assert.Contains("image GOOD", shop.Calls);
        Assert.DoesNotContain("image BAD", shop.Calls);
    }

    [Fact]
    public async Task RecordsRejectedByValidationAreReportedAsFailures()
    {
        var report = await new Loader(new FakeShop(), new StringWriter()).LoadAsync(
            Validation([Phones], [Product("A")], new RecordError("product", "BAD", "price is required")),
            "images", CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Equal("BAD", Assert.Single(report.Failures).Key);
        Assert.Equal(1, report.ProductsCreated);
    }

    [Fact]
    public async Task ProductsOfAFailedCategoryAreSkippedAndReported()
    {
        var shop = new FakeShop { Failing = { "phones" } };
        var report = await new Loader(shop, new StringWriter()).LoadAsync(
            Validation([Phones], [Product("A")]), "images", CancellationToken.None);

        Assert.DoesNotContain("product A", shop.Calls);
        Assert.Equal(["phones", "A"], report.Failures.Select(f => f.Key));
    }

    [Fact]
    public async Task ChildrenOfAFailedCategoryAreSkippedAndReported()
    {
        var shop = new FakeShop { Failing = { "all" } };
        var child = new CategoryRecord("phones", "Телефоны", "all");
        var report = await new Loader(shop, new StringWriter()).LoadAsync(
            Validation([new CategoryRecord("all", "Всё", null), child], []), "images", CancellationToken.None);

        Assert.DoesNotContain("category phones", shop.Calls);
        Assert.Equal(2, report.Failures.Count);
    }

    [Fact]
    public void SummaryMentionsCountsAndFailures()
    {
        var report = new LoadReport();
        report.Category(created: true);
        report.Product(created: false);
        report.AddFailure(new RecordError("product", "X", "bad"));

        var summary = report.Summary();

        Assert.Contains("1 created", summary);
        Assert.Contains("Failed records: 1", summary);
    }
}
