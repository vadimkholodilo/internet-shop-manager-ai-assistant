namespace ShopClient;

/// <summary>
/// Argument checks every shop's client applies the same way, so a bad call fails fast with the same exception
/// whatever the shop is, before any request is made.
/// </summary>
internal static class ArgumentChecks
{
    public static void Category(CatalogCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        Text(category.Slug, nameof(category), nameof(CatalogCategory.Slug));
        Text(category.Name, nameof(category), nameof(CatalogCategory.Name));
    }

    public static void Product(CatalogProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        Text(product.Sku, nameof(product), nameof(CatalogProduct.Sku));
        Text(product.Name, nameof(product), nameof(CatalogProduct.Name));
        Text(product.Category, nameof(product), nameof(CatalogProduct.Category));
        if (product.Brand is null)
        {
            throw new ArgumentException("The product's Brand is null.", nameof(product));
        }
        if (product.Description is null)
        {
            throw new ArgumentException("The product's Description is null.", nameof(product));
        }
        if (product.Attributes is null)
        {
            throw new ArgumentException("The product's Attributes are null; pass an empty dictionary for none.", nameof(product));
        }
        if (product.Price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(product), product.Price, "The product's Price must not be negative.");
        }
        if (product.Quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(product), product.Quantity, "The product's Quantity must not be negative.");
        }
    }

    private static void Text(string? value, string parameter, string member)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The {member} of the {parameter} is null, empty or blank.", parameter);
        }
    }
}
