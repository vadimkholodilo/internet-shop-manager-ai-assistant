# Data Model: Shared Shop Client Library

Types in the library's public surface. No persistent storage. Field rules are carried over unchanged from the test-shop
feature's product file model (`specs/001-test-shop/data-model.md`); the library does not validate them (the loader's
validator does), the shop does.

## CatalogCategory

| Field | Type | Notes |
|-------|------|-------|
| Slug | string | Stable key; the shop's category is created or updated by it |
| Name | string | |
| Parent | string? | Slug of the parent category, or none |

## CatalogProduct

| Field | Type | Notes |
|-------|------|-------|
| Sku | string | Stable key |
| Name | string | |
| Category | string | Slug of an existing category |
| Brand | string | |
| Description | string | |
| Attributes | map of string to string | Attribute name to value |
| Price | decimal | In rubles |
| Quantity | integer | Stock; 0 or more |

The image is not a field: it is uploaded separately by path (`UploadImageAsync(sku, imagePath)`).

## UpsertResult

| Field | Type | Notes |
|-------|------|-------|
| Id | long | The shop's numeric id for the item |
| Created | bool | True when created, false when updated |

## ShopType

A named, supported shop kind. Initially one value: `opencart` (name matched case-insensitively). Provides parsing and
the list of supported names. `default(ShopType)` has no name and is invalid: the factory rejects it with
`UnsupportedShopException` listing the supported names.

## ShopConnection

| Field | Type | Notes |
|-------|------|-------|
| Url | Uri | Address of the shop |
| ApiKey | string | Credential for the shop's API; never logged |
| RequestTimeout | TimeSpan | One attempt's limit; defaults to 60 seconds on the connection itself and can be overridden; must be positive |
| MaxRetries | int | Retries after the first attempt for temporary failures; default 3, 0 turns retries off; must not be negative |
| RetryDelay | TimeSpan | Wait before the first retry, doubling for each further retry with jitter, capped at 10 seconds; default 0.5 seconds; must not be negative |

The connection checks its values when built: absolute http or https `Url`, non-blank `ApiKey`; its text form hides the key.

## Failures

All derive from `ShopClientException`. `CatalogException` and `ShopUnavailableException` also carry the HTTP status (when the shop answered) and the underlying error. Bad arguments raise the standard argument exceptions instead.

| Type | Meaning | Caller reaction |
|------|---------|-----------------|
| CatalogException | The shop rejected one record, answered with something that is not JSON, or an image file cannot be read | Report it, continue |
| ShopUnavailableException | The shop cannot be used at all (unreachable, timed out, wrong key, 408/429/502/503/504), after retries | Stop |
| UnsupportedShopException | Unknown shop type, or a capability the shop's client lacks | Usage error |

## Relationships

`ICatalogLoadingClient` extends `IShopClient`; `ShopClientFactory` turns a `ShopType` and a `ShopConnection` into either.
The loader maps its file records (`CategoryRecord`, `ProductRecord`) to `CatalogCategory` and `CatalogProduct`.
