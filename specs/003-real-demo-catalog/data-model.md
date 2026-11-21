# Data Model: Real Demo Catalog

## Input

**Dump record** (one JSON line in `basket-NN.json.zst`): `imt_id`, `nm_id`, `imt_name`, `subj_name`, `subj_root_name`,
`nm_colors_names`, `vendor_code`, `description`, `brand_name`. Used only for selection (`subj_name`) and deduplication
(`imt_id`); everything else comes from the card.

**Card** (`…/info/ru/card.json`): fields used are `imt_name`, `description`, `subj_name`, `subj_root_name`,
`selling.brand_name`, `grouped_options[].group_name` / `options[].{name,value}`, `options[]` (fallback),
`media.photo_count`.

## Output

The product file from spec 001, unchanged
([schema](../001-test-shop/contracts/product-file.schema.json)).

| Product file field | From |
|--------------------|------|
| `generated` | `false` |
| `source` | `"Wildberries: Hugging Face dump nyuuzyou/wb-products (CC0) + public card files, fetched <date>"` |
| `categories[]` | `{slug: translit(subj_root_name), name: subj_root_name}` and `{slug: translit(subj_name), name: subj_name, parent: <root slug>}` |
| `sku` | `WB-{nm_id}` |
| `name` | card `imt_name` |
| `category` | child category slug |
| `brand` | card `selling.brand_name` (trimmed; required) |
| `description` | card `description` (trimmed; required) |
| `attributes` | flattened characteristics (research R8) |
| `price` | generated, seeded by `nm_id` (R8) |
| `quantity` | generated, seeded by `nm_id` (R8) |
| `image` | `wb-{nm_id}.jpg` in `images/` |

Products are written sorted by category slug, then SKU, so the file diffs cleanly.

## Validation rules (before writing)

- Schema-valid; SKUs and category slugs unique; every parent and image exists.
- With `--limit 100`: 100 products, ≥5 child categories, ≥10 brands, at least one product with `quantity = 0` and one
  with `quantity > 0`; otherwise the tool reports what is short and exits non-zero.

## Cache

`<cache>/cards/<nm_id>.json`, `<cache>/photos/<nm_id>.webp`, `<cache>/missing/<nm_id>`, `<cache>/hosts.json`
(`{ "<vol>": "<NN>" }`).
