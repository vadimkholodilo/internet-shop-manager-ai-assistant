# Contract: Catalog Upsert Endpoint (OpenCart extension)

Internal contract between the loader and the `catalog_api` OpenCart extension. It is shop-specific by
design; the shop-neutral connector contract comes in a later feature. Exact route is **to be confirmed**
during implementation (see research R1).

## Authentication

`Authorization: Bearer <api key>`. The key is created at first start from `SHOP_API_KEY`
(non-secret default for local use in `.env.example`). Missing or wrong key returns `401` and changes nothing.

## Identifiers

Requests address items by their stable keys (`slug` for categories, `sku` for products), so the loader
never needs shop ids. Every response that describes an item includes both the shop's numeric `id` and the
stable key, because this extension is expected to grow into the REST API for the rest of the shop and its
consumers will need ids to refer to items.

## Representations

Category: `{ "id": 12, "slug": "smartphones", "name": "Смартфоны", "parent_id": 3, "parent_slug": "electronics" }`

Product: `{ "id": 345, "sku": "SM-0001", "name": "...", "category_id": 12, "category_slug": "smartphones",
"brand": "...", "description": "...", "attributes": { }, "price": 49990.00, "quantity": 5,
"image_url": "..." }`

`parent_id` and `parent_slug` are `null` for top-level categories.

## Operations

| Operation | Request | Success | Errors |
|-----------|---------|---------|--------|
| Upsert category | `PUT /catalog-api/categories/{slug}` body: `name`, `parent` (slug) | `200` category representation plus `"created": bool` | `400` invalid, `404` unknown parent, `401` |
| Get category | `GET /catalog-api/categories/{slug}` or `GET /catalog-api/categories/by-id/{id}` | `200` category representation | `404`, `401` |
| List categories | `GET /catalog-api/categories?page=n` | `200` `{ items, total }` of category representations | `401` |
| Upsert product | `PUT /catalog-api/products/{sku}` body: `name`, `category` (slug), `brand`, `description`, `attributes`, `price`, `quantity` | `200` product representation plus `"created": bool` | `400` invalid, `404` unknown category, `401` |
| Get product | `GET /catalog-api/products/{sku}` or `GET /catalog-api/products/by-id/{id}` | `200` product representation | `404`, `401` |
| List products | `GET /catalog-api/products?page=n` | `200` `{ items, total }` of product representations | `401` |
| Upload image | `PUT /catalog-api/products/{sku}/image` body: image bytes, `Content-Type` | `200` product representation | `404` unknown product, `415` bad type, `401` |

## Rules

- Upserts are keyed by `slug` and `sku`; repeating a call with the same data changes nothing.
- Ids are assigned by the shop, never accepted in requests, and stay the same across upserts of the same item.
- Text is UTF-8; Cyrillic must round-trip unchanged.
- The get and list operations exist so tests and the loader can verify results without reading the database.
