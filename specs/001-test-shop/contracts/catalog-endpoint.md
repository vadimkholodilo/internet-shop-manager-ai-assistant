# Contract: Catalog Upsert Endpoint (OpenCart extension)

Internal contract between the loader and the `catalog_api` OpenCart extension. It is shop-specific by
design; the shop-neutral connector contract comes in a later feature. Exact route is **to be confirmed**
during implementation (see research R1).

## Authentication

`Authorization: Bearer <api key>`. The key is created at first start from `SHOP_API_KEY`
(non-secret default for local use in `.env.example`). Missing or wrong key returns `401` and changes nothing.

## Operations

| Operation | Request | Success | Errors |
|-----------|---------|---------|--------|
| Upsert category | `PUT /catalog-api/categories/{slug}` body: `name`, `parent` | `200` created or updated, body `{ "id": n, "created": bool }` | `400` invalid, `404` unknown parent, `401` |
| Upsert product | `PUT /catalog-api/products/{sku}` body: `name`, `category`, `brand`, `description`, `attributes`, `price`, `quantity` | `200` as above | `400` invalid, `404` unknown category, `401` |
| Upload image | `PUT /catalog-api/products/{sku}/image` body: image bytes, `Content-Type` | `200` | `404` unknown product, `415` bad type, `401` |
| List products | `GET /catalog-api/products?page=n` | `200` `{ items, total }` with `sku`, `name`, `price`, `quantity` | `401` |

## Rules

- Upserts are keyed by `slug` and `sku`; repeating a call with the same data changes nothing.
- Text is UTF-8; Cyrillic must round-trip unchanged.
- The listing exists so tests and the loader can verify results without reading the database.
