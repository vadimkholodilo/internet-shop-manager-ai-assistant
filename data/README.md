# Sample data

`sample-catalog.json` is the demo catalog the shop is seeded with. It is **generated** by
[`tools/generate-sample/generate.cs`](../tools/generate-sample/generate.cs): all brands and products are invented,
and nothing is copied from a real retailer. The photos in `images/` are placeholder pictures drawn by the same
generator, so there are no third-party image licences to worry about.

Regenerate (the output is deterministic):

```bash
dotnet run tools/generate-sample/generate.cs
```

## File format

The format is described by [`product-file.schema.json`](../specs/001-test-shop/contracts/product-file.schema.json).

```json
{
  "generated": true,
  "source": "where the data came from",
  "categories": [
    { "slug": "electronics", "name": "Электроника" },
    { "slug": "smartphones", "name": "Смартфоны", "parent": "electronics" }
  ],
  "products": [
    {
      "sku": "PH-001",
      "name": "Смартфон Norda Pulse 5 128 ГБ",
      "category": "smartphones",
      "brand": "Norda",
      "description": "Компактный смартфон для повседневных задач.",
      "attributes": { "Память": "128 ГБ", "Диагональ экрана": "6.1\"" },
      "price": 24990,
      "quantity": 14,
      "image": "ph-001.png"
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
