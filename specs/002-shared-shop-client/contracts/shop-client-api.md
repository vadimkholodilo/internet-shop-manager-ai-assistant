# Contract: ShopClient library public surface

The shop-neutral API of `src/ShopClient`. Nothing here names OpenCart; OpenCart appears only as a `ShopType` value.
Signatures describe intent; exact names are settled in implementation but the shape is fixed. All members are in the
`ShopClient` namespace. The OpenCart client's HTTP contract is unchanged and documented in
[`specs/001-test-shop/contracts/catalog-endpoint.md`](../../001-test-shop/contracts/catalog-endpoint.md).

```csharp
public interface IShopClient : IDisposable
{
    // Returns when the shop answers; throws ShopUnavailableException after `timeout` or on a rejected API key.
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

public interface ICatalogLoadingClient : IShopClient
{
    Task<UpsertResult> UpsertCategoryAsync(CatalogCategory category, CancellationToken cancellationToken = default);
    Task<UpsertResult> UpsertProductAsync(CatalogProduct product, CancellationToken cancellationToken = default);
    // Path to a local file: png, jpg/jpeg or webp. Other types throw CatalogException.
    Task UploadImageAsync(string sku, string imagePath, CancellationToken cancellationToken = default);
}

public static class ShopClientFactory
{
    public static IShopClient CreateShopClient(ShopType type, ShopConnection connection);
    public static ICatalogLoadingClient CreateCatalogLoadingClient(ShopType type, ShopConnection connection);
}

public readonly record struct ShopType
{
    public static ShopType OpenCart { get; }
    public static IReadOnlyList<string> SupportedNames { get; }
    public static bool TryParse(string? name, out ShopType type);   // case-insensitive
}

public sealed record ShopConnection(Uri Url, string ApiKey)
{
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);   // overridable per connection
}
public sealed record UpsertResult(long Id, bool Created);
public sealed record CatalogCategory(string Slug, string Name, string? Parent);
public sealed record CatalogProduct(string Sku, string Name, string Category, string Brand, string Description,
    IReadOnlyDictionary<string, string> Attributes, decimal Price, int Quantity);

public sealed class CatalogException(string message) : Exception(message);
public sealed class ShopUnavailableException(string message) : Exception(message);
public sealed class UnsupportedShopException(string message) : Exception(message);
```

## Conventions

Every async method takes `CancellationToken cancellationToken = default`, so a caller that has no token to pass
does not need to write `CancellationToken.None`.

## Behavioral guarantees

- Upserts are idempotent: the same key updates, never duplicates; `Created` tells which happened.
- A rejected single record raises `CatalogException`; a lost connection, timeout or `401` raises `ShopUnavailableException`. Messages are the same as the loader printed before.
- The library never opens a database connection and holds no static shop state, so several clients for different shops can coexist.

## Loader command line (changed surface)

New optional option `--shop <type>`; default `opencart`. Unsupported value: exit code 2 and a message listing
`ShopType.SupportedNames`. All other options, exit codes and messages are unchanged.
