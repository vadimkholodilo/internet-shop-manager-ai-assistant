# Contract: Catalog Upsert Endpoint (OpenCart extension)

Internal contract between the loader and the `ai_assistant` OpenCart extension. It is shop-specific by
design; the shop-neutral connector contract comes in a later feature. The extension answers under
`/catalog-api/` (an Apache rewrite to the controller route `extension/ai_assistant/api`, see research R1).

## Authentication

`Authorization: Bearer <api key>`. The key is the shop container's `SHOP_API_KEY` environment variable
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
| Upsert product | `PUT /catalog-api/products/{sku}` body: `name`, `category` (slug), `brand`, `description`, `attributes`, `price`, `quantity` | `200` product representation plus `"created": bool` | `400` invalid, `404` unknown category, `409` SKU maps to the URL keyword of another product, `401` |
| Get product | `GET /catalog-api/products/{sku}` or `GET /catalog-api/products/by-id/{id}` | `200` product representation | `404`, `401` |
| List products | `GET /catalog-api/products?page=n` | `200` `{ items, total }` of product representations | `401` |
| Upload image | `PUT /catalog-api/products/{sku}/image` body: image bytes, `Content-Type` | `200` product representation | `404` unknown product, `415` bad type, `400` not a valid image, `401` |

## Rules

- Upserts are keyed by `slug` and `sku`; repeating a call with the same data changes nothing.
- Writes to the same item are done one at a time (the extension takes a named lock per URL keyword), so repeating or overlapping a write never creates a second item. If the lock is not obtained within 10 seconds the answer is `423`, and the caller may repeat the request. Added by spec 002 (retries).
- Ids are assigned by the shop, never accepted in requests, and stay the same across upserts of the same item.
- Text is UTF-8; Cyrillic and markup characters such as `<` must round-trip unchanged. The shop stores text HTML-encoded and the API decodes it, so catalog text can never become markup on the storefront.
- A SKU becomes the product's URL keyword and photo file name (lower case, anything but letters and digits replaced by `-`); two SKUs that map to the same keyword conflict (`409`). SKUs are compared ignoring case.
- Updating a product keeps the settings the API does not manage (status, sort order, availability date, weights and so on) and its photos, and replaces its category and attributes with the ones sent. Discounts, specials, options and other related records are not managed: an update removes them.
- Lists are paged by 50 items (`?page=n`, from 1).
- The get and list operations exist so tests and the loader can verify results without reading the database.
