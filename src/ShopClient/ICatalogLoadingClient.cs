namespace ShopClient;

/// <summary>Filling a shop's catalog. Upserts are idempotent: the same key updates, never duplicates.</summary>
public interface ICatalogLoadingClient : IShopClient
{
    /// <exception cref="CatalogException">The shop rejected this category.</exception>
    Task<UpsertResult> UpsertCategoryAsync(CatalogCategory category, CancellationToken cancellationToken = default);

    /// <exception cref="CatalogException">The shop rejected this product.</exception>
    Task<UpsertResult> UpsertProductAsync(CatalogProduct product, CancellationToken cancellationToken = default);

    /// <param name="imagePath">Path to a local png, jpg/jpeg or webp file.</param>
    /// <exception cref="CatalogException">The image type is not supported or the shop rejected the image.</exception>
    Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken = default);
}
