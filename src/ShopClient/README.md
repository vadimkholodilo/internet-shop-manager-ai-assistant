# ShopClient

The shared library that talks to a shop through its API. It holds everything shop-specific on the client side, so the
programs that use it (the product loader today, the shop connector later) contain no shop-specific code, and a new
shop needs only a new client here. It never touches a shop's database.

## Contracts

| Interface | For | Operations |
|-----------|-----|------------|
| `IShopClient` | any consumer | `WaitUntilReadyAsync` |
| `ICatalogLoadingClient : IShopClient` | filling a catalog (the loader only) | `UpsertCategoryAsync`, `UpsertProductAsync`, `UploadImageAsync` (takes a path to a local file) |

Upserts are idempotent: the same key updates and never duplicates, and `UpsertResult.Created` tells which happened.
Every async method takes an optional `CancellationToken`.

## Using it

```csharp
using var client = ShopClientFactory.CreateCatalogLoadingClient(
    ShopType.OpenCart, new ShopConnection(new Uri("http://localhost:8080"), apiKey));
await client.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));
var result = await client.UpsertCategoryAsync(new CatalogCategory("phones", "Телефоны", null));
```

Callers never name a client class; they pick a `ShopType` (the loader reads it from `--shop`, default `opencart`).
`ShopType.SupportedNames` lists the supported shops.

## Failures

Everything the library reports about a shop derives from `ShopClientException`, so one `catch` covers it all.

| Exception | Meaning | Caller reaction |
|-----------|---------|-----------------|
| `CatalogException` | the shop rejected one record, answered with something that is not valid JSON, or an image file could not be read; `StatusCode` holds the HTTP status when there was an answer | report it and continue |
| `ShopUnavailableException` | the shop cannot be used at all (unreachable, timed out, wrong API key, 408/429/502/503/504); `StatusCode` is set when it answered, `InnerException` holds the network error | stop |
| `UnsupportedShopException` | unknown shop type, or a capability the shop's client lacks | usage error |

A bad argument is a programming error, not a shop failure, and raises the usual `ArgumentNullException`,
`ArgumentException` or `ArgumentOutOfRangeException` before any request is made. `ShopConnection` checks its values when it
is built (absolute http(s) URL, non-blank API key, positive `RequestTimeout`), and its `ToString()` hides the API key.
Cancelling through the token raises `OperationCanceledException`, never `ShopUnavailableException`.

`WaitUntilReadyAsync(timeout)` tries at least once and returns or throws within `timeout` (plus at most one request), however long the shop
takes to answer each attempt.

## Retries

Requests that fail in a way that may be temporary (lost connection, timeout of one attempt, HTTP 408, 423, 429, 500, 502, 503, 504)
are repeated up to `ShopConnection.MaxRetries` times (default 3; 0 turns retries off). The wait starts at
`ShopConnection.RetryDelay` (default 0.5 s), doubles each time with jitter, is capped at 10 s, and honors `Retry-After` exactly up to 60 s (a longer request ends the retrying).
A rejected record (400, 404, 409, 415), a rejected API key (401), an unknown host, a TLS failure and cancellation are never retried. When the attempts are
used up you get the same exception as without retries, with the number of attempts in its message.

This is safe because every request is idempotent by the item's key (SKU or slug), and the OpenCart catalog API serializes
writes to one key so that overlapping repeats cannot create a second item. Two things to know: a repeated write whose first
attempt had already succeeded reports the item as updated, not created, and a request against a shop that hangs can take up
to `(MaxRetries + 1) × RequestTimeout` before it fails. The helper is `HttpRetry.cs`; another HTTP-based client uses it
the same way (build a fresh request per attempt inside the function you pass).

## Adding a shop

1. Add a folder next to `OpenCart/` with an `internal` client implementing `IShopClient`, and `ICatalogLoadingClient` if the shop can be loaded. Keep every shop-specific address, header and response format inside it.
2. Add a `ShopType` value and its name to `ShopType.SupportedNames`, and create the client in `ShopClientFactory`.
3. Test it like the others: unit tests in `tests/ShopClient.UnitTests` for logic that needs no shop, and `ShopClientTests` in `tests/ShopLoader.IntegrationTests` against the real shop (no stand-ins).

The shop-neutral API is described in [`specs/002-shared-shop-client/contracts/shop-client-api.md`](../../specs/002-shared-shop-client/contracts/shop-client-api.md).
The OpenCart client uses the catalog API extension from [`shop/extension/ai_assistant/`](../../shop/extension/ai_assistant/).
