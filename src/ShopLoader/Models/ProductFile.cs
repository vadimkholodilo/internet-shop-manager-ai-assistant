namespace ShopLoader.Models;

/// <summary>A category as it appears in the product file. <see cref="Slug"/> is its stable key.</summary>
internal sealed record CategoryRecord(string Slug, string Name, string? Parent);

/// <summary>
/// A product as it appears in the product file. <see cref="Sku"/> is its stable key.
/// Rules (from data-model.md): price greater than 0 in rubles; quantity an integer of 0 or more;
/// image a path relative to the images directory whose file must exist.
/// </summary>
internal sealed record ProductRecord(
    string Sku,
    string Name,
    string Category,
    string Brand,
    string Description,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    int Quantity,
    string Image);

/// <summary>The part of the product file that passed validation.</summary>
internal sealed record ProductFile(
    IReadOnlyList<CategoryRecord> Categories,
    IReadOnlyList<ProductRecord> Products);
