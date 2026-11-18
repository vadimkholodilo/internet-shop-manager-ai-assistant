# Contract: ShopClient library public surface

The shop-neutral API of `src/ShopClient`. Nothing here names OpenCart; OpenCart appears only as a `ShopType` value.
Signatures describe intent; exact names are settled in implementation but the shape is fixed. All members are in the
`ShopClient` namespace. The OpenCart client's HTTP contract is unchanged and documented in
[`specs/001-test-shop/contracts/catalog-endpoint.md`](../../001-test-shop/contracts/catalog-endpoint.md).

```csharp
public interface IShopClient : IDisposable
{
    // Returns when the shop answers; throws ShopUnavailableException after `timeout` in total or on a rejected API key.
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

public interface ICatalogLoadingClient : IShopClient
{
    Task<UpsertResult> UpsertCategoryAsync(CatalogCategory category, CancellationToken cancellationToken = default);
    Task<UpsertResult> UpsertProductAsync(CatalogProduct product, CancellationToken cancellationToken = default);
    // Path to a local file: png, jpg/jpeg or webp. Other types, and files that cannot be read, throw CatalogException.
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

public sealed record ShopConnection
{
    // Validated on construction: absolute http(s) Url, non-blank ApiKey (ArgumentException family); ToString hides the key.
    public ShopConnection(Uri url, string apiKey);
    public Uri Url { get; }
    public string ApiKey { get; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);   // per attempt; overridable; must be positive
    public int MaxRetries { get; init; } = 3;                                    // retries after the first attempt; 0 = none
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);  // before the first retry; doubles, jittered, capped at 10 s
}
public sealed record UpsertResult(long Id, bool Created);
public sealed record CatalogCategory(string Slug, string Name, string? Parent);
public sealed record CatalogProduct(string Sku, string Name, string Category, string Brand, string Description,
    IReadOnlyDictionary<string, string> Attributes, decimal Price, int Quantity);

public abstract class ShopClientException(string message, Exception? innerException = null) : Exception(message, innerException);
public sealed class CatalogException : ShopClientException { public HttpStatusCode? StatusCode { get; } }
public sealed class ShopUnavailableException : ShopClientException { public HttpStatusCode? StatusCode { get; } }
public sealed class UnsupportedShopException : ShopClientException;
```

## Conventions

Every async method takes `CancellationToken cancellationToken = default`, so a caller that has no token to pass
does not need to write `CancellationToken.None`.

## Behavioral guarantees

- Upserts are idempotent: the same key updates, never duplicates; `Created` tells which happened.
- A rejected single record, an unreadable image file or a non-JSON answer raises `CatalogException`; a lost connection, timeout, `401` or `502/503/504` raises `ShopUnavailableException`. Both carry the HTTP status when there was an answer and the original exception as `InnerException`.
- A bad argument (null, blank key fields, negative price or quantity, non-positive timeout) raises the `ArgumentException` family before any request is made.
- Temporary failures (lost connection, attempt timeout, 408/423/429/500/502/503/504) are retried up to `MaxRetries` times with exponential backoff and jitter; the final failure is the one that would have been thrown without retries, naming the attempts. Rejections of the record, `401`, unknown hosts, TLS failures and cancellation are never retried.
- The OpenCart catalog API serializes writes per item key, so a repeated write never creates a second item.
- `WaitUntilReadyAsync(timeout)` tries at least once and does not run past `timeout` because of a slow attempt.
- The library never opens a database connection and holds no static shop state, so several clients for different shops can coexist.

## Loader command line (changed surface)

New optional option `--shop <type>`; default `opencart`. Unsupported value: exit code 2 and a message listing
`ShopType.SupportedNames`. All other options, exit codes and messages are unchanged.
