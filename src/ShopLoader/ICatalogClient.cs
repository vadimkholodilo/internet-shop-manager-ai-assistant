using ShopLoader.Models;

namespace ShopLoader;

/// <summary>Result of an upsert: the shop's id for the item and whether it was created or updated.</summary>
internal sealed record UpsertResult(long Id, bool Created);

/// <summary>What the loader needs from a shop. OpenCart specifics stay behind this interface.</summary>
internal interface ICatalogClient
{
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task<UpsertResult> UpsertCategoryAsync(CategoryRecord category, CancellationToken cancellationToken);

    Task<UpsertResult> UpsertProductAsync(ProductRecord product, CancellationToken cancellationToken);

    Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken);
}

/// <summary>Raised when the shop rejects a single record; the loader reports it and continues.</summary>
internal sealed class CatalogException(string message) : Exception(message);

/// <summary>Raised when the shop cannot be used at all (unreachable, wrong credentials); the loader stops.</summary>
internal sealed class ShopUnavailableException(string message) : Exception(message);
