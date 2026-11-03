using ShopLoader.Validation;

namespace ShopLoader.UnitTests;

public sealed class ProductFileValidatorTests : IDisposable
{
    private readonly TestDirectory directory = new();

    public void Dispose() => directory.Dispose();

    private static string Product(
        string sku = "A-1", string price = "100", string quantity = "1", string category = "phones",
        string image = "a.png", string extra = "") =>
        $$"""
        {"sku":"{{sku}}","name":"Смартфон","category":"{{category}}","brand":"Acme","description":"Описание",
         "attributes":{"Память":"128 ГБ"},"price":{{price}},"quantity":{{quantity}},"image":"{{image}}"{{extra}}}
        """;

    private ValidationResult Validate(string products, string categories = """[{"slug":"phones","name":"Телефоны"}]""")
    {
        directory.AddImage("a.png");
        var file = directory.WriteCatalog($$"""{"categories":{{categories}},"products":[{{products}}]}""");
        return ProductFileValidator.ValidateFile(file, directory.Images);
    }

    [Fact]
    public void ValidFileProducesNoErrors()
    {
        var result = Validate(Product());

        Assert.Empty(result.Errors);
        var product = Assert.Single(result.File.Products);
        Assert.Equal("A-1", product.Sku);
        Assert.Equal(100m, product.Price);
        Assert.Equal("128 ГБ", product.Attributes["Память"]);
    }

    [Fact]
    public void MissingFileFailsBeforeAnyChange()
    {
        var e = Assert.Throws<ProductFileException>(() => ProductFileValidator.ValidateFile(Path.Combine(directory.Path, "none.json"), directory.Images));
        Assert.Contains("not found", e.Message);
    }

    [Fact]
    public void InvalidJsonFailsBeforeAnyChange()
    {
        var file = directory.WriteCatalog("{ not json");
        var e = Assert.Throws<ProductFileException>(() => ProductFileValidator.ValidateFile(file, directory.Images));
        Assert.Contains("not valid JSON", e.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"categories":[]}""")]
    [InlineData("""{"categories":{},"products":[]}""")]
    public void WrongFormatFailsBeforeAnyChange(string json)
    {
        var file = directory.WriteCatalog(json);
        Assert.Throws<ProductFileException>(() => ProductFileValidator.ValidateFile(file, directory.Images));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("\"100\"")]
    public void PriceMustBeANumberGreaterThanZero(string price)
    {
        var result = Validate(Product(price: price));

        Assert.Empty(result.File.Products);
        Assert.Contains("price", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"3\"")]
    public void QuantityMustBeAnIntegerOfZeroOrMore(string quantity)
    {
        var result = Validate(Product(quantity: quantity));

        Assert.Empty(result.File.Products);
        Assert.Contains("quantity", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ZeroQuantityIsValid()
    {
        Assert.Empty(Validate(Product(quantity: "0")).Errors);
    }

    [Fact]
    public void MissingImageFileIsReportedAndSkipped()
    {
        var result = Validate(Product(image: "missing.png"));

        Assert.Empty(result.File.Products);
        Assert.Contains("image file not found", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ImagePathCannotEscapeTheImagesDirectory()
    {
        File.WriteAllBytes(Path.Combine(directory.Path, "secret.png"), [1]);
        var result = Validate(Product(image: "../secret.png"));

        Assert.Empty(result.File.Products);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void UnknownCategoryIsReported()
    {
        var result = Validate(Product(category: "tablets"));

        Assert.Contains("unknown category", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void UnknownFieldIsReported()
    {
        var result = Validate(Product(extra: ",\"color\":\"red\""));

        Assert.Contains("unknown field", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("A/1")]
    [InlineData("A?1")]
    [InlineData("A%1")]
    public void SkuCannotContainCharactersThatBreakUrls(string sku)
    {
        var result = Validate(Product(sku: sku));

        Assert.Empty(result.File.Products);
        Assert.Contains("sku must not contain", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void DuplicateSkuKeepsTheFirstProduct()
    {
        var result = Validate(Product(price: "100") + "," + Product(price: "200"));

        var product = Assert.Single(result.File.Products);
        Assert.Equal(100m, product.Price);
        Assert.Contains("duplicate sku", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void OneBadRecordDoesNotStopTheOthers()
    {
        var result = Validate(Product(sku: "BAD", price: "0") + "," + Product(sku: "GOOD"));

        Assert.Equal("GOOD", Assert.Single(result.File.Products).Sku);
        Assert.Equal("BAD", Assert.Single(result.Errors).Key);
    }

    [Fact]
    public void CyrillicAndVeryLongValuesAreAccepted()
    {
        var longName = new string('я', 5000);
        var result = Validate(Product().Replace("Смартфон", longName));

        Assert.Empty(result.Errors);
        Assert.Equal(longName, Assert.Single(result.File.Products).Name);
    }

    [Fact]
    public void ParentCategoriesComeBeforeChildren()
    {
        var result = Validate(Product(), """[{"slug":"phones","name":"Телефоны","parent":"all"},{"slug":"all","name":"Всё"}]""");

        Assert.Empty(result.Errors);
        Assert.Equal(["all", "phones"], result.File.Categories.Select(c => c.Slug));
    }

    [Fact]
    public void CategoryWithUnknownParentIsReported()
    {
        var result = Validate(Product(), """[{"slug":"phones","name":"Телефоны","parent":"nope"}]""");

        Assert.Empty(result.File.Categories);
        Assert.Contains(result.Errors, e => e.Key == "phones" && e.Message.Contains("unknown parent"));
    }

    [Fact]
    public void CircularParentsAreReported()
    {
        var result = Validate(Product(), """[{"slug":"a","name":"A","parent":"b"},{"slug":"b","name":"B","parent":"a"}]""");

        Assert.Empty(result.File.Categories);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData("Phones")]
    [InlineData("a b")]
    [InlineData("")]
    public void InvalidCategorySlugIsReported(string slug)
    {
        var result = Validate(Product(), $$"""[{"slug":"{{slug}}","name":"X"}]""");

        Assert.Empty(result.File.Categories);
        Assert.NotEmpty(result.Errors);
    }
}
