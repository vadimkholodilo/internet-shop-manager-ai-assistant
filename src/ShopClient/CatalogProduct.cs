namespace ShopClient;

/// <summary>
/// A product of a shop's catalog. <see cref="Sku"/> is its stable key. The image is not a field: it is uploaded
/// separately by <see cref="ICatalogLoadingClient.UploadImageAsync"/>.
/// </summary>
/// <param name="Category">Slug of a category that exists in the shop.</param>
/// <param name="Price">Price in rubles.</param>
/// <param name="Quantity">Stock; an integer of 0 or more.</param>
public sealed record CatalogProduct(
    string Sku,
    string Name,
    string Category,
    string Brand,
    string Description,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    int Quantity);
