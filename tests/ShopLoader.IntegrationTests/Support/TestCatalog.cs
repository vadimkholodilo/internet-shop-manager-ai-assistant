using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>
/// A small product file in a temporary directory, with unique category slugs and SKUs so tests sharing
/// one shop never interfere. Photos are copies of the generated sample photos.
/// </summary>
public sealed class TestCatalog : IDisposable
{
    private readonly JsonArray categories = [];
    private readonly JsonArray products = [];

    public string Prefix { get; } = "t" + Guid.NewGuid().ToString("N")[..8];

    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "shop-it-" + Guid.NewGuid().ToString("N"));

    public string File => Path.Combine(Directory, "catalog.json");

    public string Images => Path.Combine(Directory, "images");

    public string CategorySlug => $"{Prefix}-cat";

    public TestCatalog(string repositoryRoot)
    {
        System.IO.Directory.CreateDirectory(Images);
        System.IO.File.Copy(Path.Combine(repositoryRoot, "data", "images", "ph-001.png"), Path.Combine(Images, "photo.png"));
        categories.Add(new JsonObject { ["slug"] = CategorySlug, ["name"] = "Тестовая категория" });
    }

    public string Sku(int number) => $"{Prefix.ToUpperInvariant()}-{number:000}";

    public TestCatalog AddProduct(int number, decimal price = 1000m, int quantity = 5, string? name = null, string? description = null,
        JsonObject? attributes = null)
    {
        products.Add(Product(number, price, quantity, name, description, attributes));
        return this;
    }

    public JsonObject Product(int number, decimal price = 1000m, int quantity = 5, string? name = null, string? description = null,
        JsonObject? attributes = null) =>
        new()
        {
            ["sku"] = Sku(number),
            ["name"] = name ?? $"Тестовый товар {number}",
            ["category"] = CategorySlug,
            ["brand"] = $"Бренд {Prefix}",
            ["description"] = description ?? "Описание тестового товара",
            ["attributes"] = attributes ?? new JsonObject { ["Цвет"] = "Чёрный" },
            ["price"] = price,
            ["quantity"] = quantity,
            ["image"] = "photo.png",
        };

    public TestCatalog AddRaw(JsonObject product)
    {
        products.Add(product);
        return this;
    }

    /// <summary>Writes the file (replacing an earlier version) and returns its path.</summary>
    public string Write()
    {
        var root = new JsonObject { ["categories"] = categories.DeepClone(), ["products"] = products.DeepClone() };
        System.IO.File.WriteAllText(File, root.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return File;
    }

    public void SetPrice(int number, decimal price)
    {
        foreach (var product in products)
        {
            if (product!["sku"]!.GetValue<string>() == Sku(number))
            {
                product["price"] = price;
            }
        }
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
