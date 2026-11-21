using ShopLoader.Validation;

namespace ShopLoader.UnitTests;

/// <summary>
/// The committed demo catalog (built by tools/build-catalog from public Wildberries data) is valid for the loader
/// and keeps the subset rules of specs/003-real-demo-catalog.
/// </summary>
public sealed class SampleCatalogTests
{
    private static string Data => Path.Combine(RepositoryRoot.Find(), "data");

    private static ValidationResult Load() =>
        ProductFileValidator.ValidateFile(Path.Combine(Data, "sample-catalog.json"), Path.Combine(Data, "images"));

    [Fact]
    public void SampleCatalogIsValid()
    {
        var result = Load();

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SampleCatalogIsSpreadOverCategoriesAndBrands()
    {
        var file = Load().File;

        Assert.Equal(100, file.Products.Count);
        Assert.True(file.Products.Select(p => p.Category).Distinct().Count() >= 5);
        Assert.True(file.Products.Select(p => p.Brand).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 10);
    }

    [Fact]
    public void SampleCatalogHasOutOfStockAndInStockProducts()
    {
        var products = Load().File.Products;

        Assert.Contains(products, p => p.Quantity == 0);
        Assert.Contains(products, p => p.Quantity > 0);
    }

    [Fact]
    public void SampleCatalogIsInRussianAndNamesItsSource()
    {
        var result = Load();
        var cyrillic = new System.Text.RegularExpressions.Regex("\\p{IsCyrillic}");
        var text = File.ReadAllText(Path.Combine(Data, "sample-catalog.json"));

        Assert.All(result.File.Products, p => Assert.Matches(cyrillic, p.Name + p.Description));
        Assert.All(result.File.Categories, c => Assert.Matches(cyrillic, c.Name));
        Assert.Contains("\"generated\": false", text);
        Assert.Contains("nyuuzyou/wb-products", text);
    }

    [Fact]
    public void SampleCatalogStaysUnderTenMegabytes()
    {
        var size = new DirectoryInfo(Data).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

        Assert.True(size < 10 * 1024 * 1024, $"data/ takes {size / 1024 / 1024} MB");
    }
}
