# Demo catalog

`sample-catalog.json` is the catalog the demo shop is seeded with: 100 real electronics products (smartphones,
laptops, tablets, TVs, monitors, headphones, speakers, smart watches) with their Russian names, descriptions,
characteristics and main photos in `images/`.

## Source and terms

- **Index**: the Hugging Face dataset [`nyuuzyou/wb-products`](https://huggingface.co/datasets/nyuuzyou/wb-products),
  a dump of Wildberries products collected in about 2023, published under CC0 (public domain).
- **Details**: Wildberries' public product card files (`card.json`) and main photos, fetched in November 2026 at one
  request per second.
- **Prices and stock are generated** (random but reproducible per product); they are not Wildberries prices.

The catalog is used only for a **non-commercial demo** of this project. Product texts and photos belong to their
owners (Wildberries and its sellers). Brand names are as the sellers entered them, so a few are store names rather than
manufacturers, and typos in characteristics are kept as they are. If you own any of this content and want it removed,
open an issue and it will be removed.

## Rebuild

The catalog is built by [`tools/build-catalog/build-catalog.cs`](../tools/build-catalog/build-catalog.cs); see
[its contract](../specs/003-real-demo-catalog/contracts/build-catalog-cli.md). From the repository root:

```bash
dotnet run tools/build-catalog/build-catalog.cs -- --limit 100 --out out/subset
cp out/subset/catalog.json data/sample-catalog.json && rm -rf data/images && cp -r out/subset/images data/images
```

Cards and photos are cached in `.cache/build-catalog/`, so a re-run makes no requests for products it already has and
gives the same file. The full set (3000 products: `--dumps 11-12 --limit 3000 --archive`) is kept outside the
repository.

## File format

The format is described by [`product-file.schema.json`](../specs/001-test-shop/contracts/product-file.schema.json).

```json
{
  "generated": false,
  "source": "where the data came from",
  "categories": [
    { "slug": "smartfony-i-aksessuary", "name": "Смартфоны и аксессуары" },
    { "slug": "smartfony", "name": "Смартфоны", "parent": "smartfony-i-aksessuary" }
  ],
  "products": [
    {
      "sku": "WB-165725653",
      "name": "Смартфон Redmi Note 12 6GB+128GB",
      "category": "smartfony",
      "brand": "Xiaomi",
      "description": "Описание из карточки товара.",
      "attributes": { "Диагональ экрана": "6.67\"", "Емкость аккумулятора (мА/ч)": "5000 мА*ч" },
      "price": 24990,
      "quantity": 14,
      "image": "wb-165725653.jpg"
    }
  ]
}
```

| Field | Rules |
|-------|-------|
| `categories[].slug` | Stable key, lowercase letters, digits and `-`, unique. Parents must be in the file. |
| `products[].sku` | Stable key, unique. A repeated SKU keeps the first record. |
| `products[].category` | Slug of a category in the file. |
| `products[].price` | Number greater than 0, in rubles. |
| `products[].quantity` | Integer, 0 or more. `0` means out of stock. |
| `products[].image` | File in the images directory (`images/` next to the file by default). `.png`, `.jpg` or `.webp`. |

A record that breaks a rule is reported and skipped; the rest are loaded. A missing file, invalid JSON or a
wrong overall shape stops the loader before anything is changed.
