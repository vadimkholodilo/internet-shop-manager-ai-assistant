using ShopLoader.Validation;

namespace ShopLoader.UnitTests;

public sealed class SampleCatalogTests
{
    private static ValidationResult Load()
    {
        var data = Path.Combine(RepositoryRoot.Find(), "data");
        return ProductFileValidator.ValidateFile(Path.Combine(data, "sample-catalog.json"), Path.Combine(data, "images"));
    }

    [Fact]
    public void SampleCatalogIsValid()
    {
        var result = Load();

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SampleCatalogIsBigEnough()
    {
        var file = Load().File;

        Assert.True(file.Categories.Count >= 3);
        Assert.True(file.Products.Select(p => p.Brand).Distinct().Count() >= 3);
        Assert.True(file.Products.Count >= 20);
    }

    [Fact]
    public void EveryProductHasAPriceAndAnExistingPhoto()
    {
        var images = Path.Combine(RepositoryRoot.Find(), "data", "images");

        foreach (var product in Load().File.Products)
        {
            Assert.True(product.Price > 0, product.Sku);
            Assert.True(File.Exists(Path.Combine(images, product.Image)), product.Sku);
        }
    }

    [Fact]
    public void SampleCatalogIsInRussianAndMarkedAsGenerated()
    {
        var result = Load();
        var cyrillic = new System.Text.RegularExpressions.Regex("\\p{IsCyrillic}");

        Assert.All(result.File.Products, p => Assert.Matches(cyrillic, p.Name + p.Description));
        Assert.All(result.File.Categories, c => Assert.Matches(cyrillic, c.Name));
        Assert.Contains("\"generated\": true", File.ReadAllText(Path.Combine(RepositoryRoot.Find(), "data", "sample-catalog.json")));
    }

    [Fact]
    public void SampleCatalogHasOutOfStockAndInStockProducts()
    {
        var products = Load().File.Products;

        Assert.Contains(products, p => p.Quantity == 0);
        Assert.Contains(products, p => p.Quantity > 0);
    }
}
