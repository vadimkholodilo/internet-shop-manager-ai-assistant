namespace ShopClient;

/// <summary>
/// Filling a shop's catalog. Upserts are idempotent: the same key updates, never duplicates. Besides the exceptions
/// listed per method, any call can raise <see cref="ShopUnavailableException"/> when the shop cannot be used at all
/// (unreachable, timed out, credentials rejected); the caller should stop then instead of trying the next record.
/// </summary>
public interface ICatalogLoadingClient : IShopClient
{
    /// <exception cref="ArgumentNullException"><paramref name="category"/> is null.</exception>
    /// <exception cref="ArgumentException">The slug or name is blank.</exception>
    /// <exception cref="CatalogException">The shop rejected this category.</exception>
    Task<UpsertResult> UpsertCategoryAsync(CatalogCategory category, CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentNullException"><paramref name="product"/> is null.</exception>
    /// <exception cref="ArgumentException">A required field is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The price or the quantity is negative.</exception>
    /// <exception cref="CatalogException">The shop rejected this product.</exception>
    Task<UpsertResult> UpsertProductAsync(CatalogProduct product, CancellationToken cancellationToken = default);

    /// <param name="imagePath">Path to a local png, jpg/jpeg or webp file.</param>
    /// <exception cref="ArgumentException"><paramref name="sku"/> or <paramref name="imagePath"/> is null or blank.</exception>
    /// <exception cref="CatalogException">The image type is not supported, the file cannot be read, or the shop rejected the image.</exception>
    Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken = default);
}
