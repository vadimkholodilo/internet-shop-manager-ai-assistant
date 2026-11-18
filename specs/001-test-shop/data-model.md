# Data Model: Test Electronics Shop (Local Demo)

## Product File (JSON)

| Field | Type | Rules |
|-------|------|-------|
| `generated` | bool | `true` for the sample catalog |
| `source` | string | Describes where data came from |
| `categories[]` | array | At least one |
| `products[]` | array | At least one |

Schema: [contracts/product-file.schema.json](contracts/product-file.schema.json).

## Category

| Field | Type | Rules |
|-------|------|-------|
| `slug` | string | Unique, stable key used for idempotency |
| `name` | string | Russian display name |
| `parent` | string? | Slug of the parent category |

## Product

| Field | Type | Rules |
|-------|------|-------|
| `sku` | string | Unique, stable key used for idempotency; duplicates in a file keep the first |
| `name` | string | Required, Russian |
| `category` | string | Slug of an existing category |
| `brand` | string | Required |
| `description` | string | Required |
| `attributes` | object (string to string) | Key attributes shown on the product page |
| `price` | decimal | Required, greater than 0, in rubles |
| `quantity` | integer | 0 or more; 0 means out of stock |
| `image` | string | Path relative to `data/images/`; the file must exist |

Records that break a rule are reported and skipped; the rest are loaded.

## Relationships

A product belongs to one category; a category may have one parent. Brands are values on products, not
separate entities in the file.

## State

The loader is stateless. Idempotency comes from upsert by `slug` and `sku` in the shop, so a re-run
applies changed values and creates nothing new.
