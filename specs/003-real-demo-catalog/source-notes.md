# Real catalog for the demo shop: findings and decisions

Input for the next feature (it replaces the abandoned spec 003, "DNS catalog parser"). Written 2026-11-20.

## Goal

A realistic Russian electronics catalog for the demo shop, in the existing product file format from spec 001
(`specs/001-test-shop/contracts/product-file.schema.json`), loaded by the existing loader with no changes.

- About 100 products are committed to the repository for local development and tests.
- The full set (a few thousand products) is stored outside GitHub (S3). How it reaches the loader is not a concern.

## Rejected sources

- **Scraping dns-shop.ru.** Qrator bot protection returns 403 to every browser that automation tools start:
  Playwright Chromium in Docker (headless and headed) and real Chrome on the host through Crawlee (with and without
  fingerprints). An ordinary Playwright browser session from the same IP passed, so it is browser detection, not an
  IP block. Getting around it (stealth browsers, IP rotation) is not worth it for demo data and would mean evading
  the protection.
- **Open Icecat.** Russian coverage and the list of free (sponsoring) brands are unconfirmed, and it has no prices.
- **Amazon datasets + LLM translation.** It works, but it is a US catalog and the translation costs tokens. Kept as a
  fallback only.

## Chosen source: Wildberries dump + Wildberries card files

**Index: the Hugging Face dataset `nyuuzyou/wb-products`** (CC0, public domain).

- 12 files `basket-01.json.zst` … `basket-12.json.zst`, zstd-compressed JSON Lines, 15.8 GB in total, about
  160M products, collected in about 2023. `basket-12` is the smallest (35 MB, about 197k products).
- The fields are only `imt_id`, `nm_id`, `imt_name`, `subj_name`, `subj_root_name`, `nm_colors_names`,
  `vendor_code`, `description` and `brand_name`. There are no characteristics, prices or images.
- In `basket-12`: Смартфоны 114, Наушники 226, Телевизоры 129, Мониторы 35, Ноутбуки 13, Планшеты 12, Колонки 138,
  Смарт-часы 46. Root names are, for example, `Смартфоны и аксессуары`, `Телевизоры и аудиотехника`,
  `Ноутбуки и компьютеры` and `Периферия и аксессуары`.

**Details: Wildberries' public static card files**, plain HTTP, with no bot protection.

- Card: `https://basket-{NN}.wbbasket.ru/vol{nm_id // 100000}/part{nm_id // 1000}/{nm_id}/info/ru/card.json`
- Main photo: `…/{nm_id}/images/big/1.webp` (WebP, 900×1200, about 75 KB). Further photos are `2.webp` and so on,
  up to `media.photo_count`.
- `NN` (the server) depends on the `vol` range. For `vol1656` it is `basket-12`. Probe `01`, `02`, … once per `vol`
  and cache the result.
- Check: 24 of 24 electronics products from the 2023 dump still had a card on 2026-11-20, at one request per second.
- Card fields:
  - `imt_name`, `description`, `subj_name`, `subj_root_name`, `vendor_code`, `nm_colors_names`
  - `options`: a flat list of `{name, value}`
  - `grouped_options`: groups (`group_name`, `options`), for example Основная информация, Экран, Процессор, Связь,
    Питание, Габариты
  - `selling.brand_name`
  - `media.photo_count`
  - `data.subject_id`, `data.subject_root_id`
  - `colors`: the `nm_id`s of colour variants of the same `imt_id`

  An example smartphone card has 30 characteristics, such as `Количество ядер процессора = 8`,
  `Диагональ экрана = 6.6 дюйм` and `Емкость аккумулятора (мА/ч) = 5000 мА*ч`.
- There is no price or stock in the card.

## What is needed to make it usable

1. **Select**: stream the dump files (no need to keep them) and keep the chosen `subj_name` values, for example
   Смартфоны, Ноутбуки, Планшеты, Телевизоры, Наушники, Мониторы.
2. **Deduplicate**: one product per `imt_id`, since colour variants share it.
3. **Fetch** the card and the main photo at a polite rate, caching them on disk so a re-run skips what it already
   has (that is enough for resuming).
4. **Filter noise** (it is a marketplace):
   - Accessories filed under main categories, such as a TV remote under Телевизоры. Require the category's key
     characteristics, for example a TV must have `Диагональ экрана`.
   - A missing brand: skip the product.
   - Sellers' brand names, such as `GALAXY` instead of Samsung. Optionally map brands from `Модель`.
   - Typos in values (`Andriod`, `Bletooth`) stay as they are.
5. **Map to the product file**:
   - `sku` is `WB-{nm_id}`, `name` is `imt_name`, `brand` is `selling.brand_name`, and `description` is the card's
     description.
   - Categories: `subj_root_name` becomes the parent and `subj_name` the child. Slugs must match `^[a-z0-9-]+$`, so
     transliterate the Cyrillic names.
   - Attributes: all characteristics, flattened from `grouped_options`. A name that appears in more than one group is
     prefixed `<group>: <name>`. Names are limited to 64 characters (OpenCart). Values stay text.
6. **Generate prices and stock** (it is a demo, so they can be random): a price within a plausible range per
   category, seeded by `nm_id` so re-runs give the same result. Quantity 0–50, with about 5% out of stock.
7. **Photos**: convert to JPEG with the long side at most 800 px (`<sku>.jpg`). The 100-product subset should stay
   under 10 MB.
8. **Output**: a product file plus `images/`. With a limit, products are spread over categories and brands
   (100 → at least 5 categories, at least 10 brands, some out of stock). The full set is optionally packed as one
   tar.gz.

## Decisions to carry over

- One command with an optional `--limit`; no separate raw-output or subset commands.
- The tool runs on the host with `uv` for Python; no Docker. No browser is needed any more.
- No test suite for the tool itself (it runs once). Validate the output against the schema before writing it, and
  let the existing .NET tests cover the committed subset.
- The real subset replaces the generated sample catalog at the same path (`data/sample-catalog.json` and
  `data/images/`). `tools/generate-sample` is removed.
  - Tests that name invented products ("Norda", `PH-001`, …) read their expectations from the file instead.
  - `TestCatalog` and `ShopTest.SampleImage` get their own test photo in the test project.
  - `tools/smoke-test.sh` searches by a brand taken from the file.
  - `SampleCatalogTests` checks the subset rules: size, at least 5 categories, at least 10 brands, both in and out
    of stock, under 10 MB, and the source.
- `data/README.md` states the source (the Wildberries dump, CC0, and the card files), the date, that the demo is
  non-commercial, that photos and texts belong to their owners, and that content is removed on request.

## Open question for the new spec

Python was chosen only for Crawlee's browser crawling. Without a browser, plain HTTP plus JSON fits .NET just as well
(a `dotnet run` file app, like `tools/generate-sample/generate.cs`), and the constitution says .NET first
(Principle I). Decide between .NET (no constitution deviation) and Python with `uv` (it needs a justification).
