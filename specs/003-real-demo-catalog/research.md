# Research: Real Demo Catalog

Source research (rejected sources, card URLs, card fields, noise) is in `catalog-dataset-notes.md` at the repository
root and is not repeated here. This file records the implementation decisions.

## R1. Stack

- **Decision**: .NET 10 file-based app, `tools/build-catalog/build-catalog.cs`, with `#:package` directives and
  `#:property PublishAot=false`.
- **Rationale**: Python was considered only for browser crawling, which is no longer needed; plain HTTP and JSON fit
  .NET, so Principle I needs no exception. Same form as the previous `generate.cs`.
- **Alternatives**: Python with `uv` (needs a Principle I justification, gains nothing); a project in the solution
  (built by CI for a tool that runs once).

## R2. Reading the dump

- **Decision**: `ZstdSharp.Port` 0.8.8 `DecompressionStream` over the HTTP response stream of
  `https://huggingface.co/datasets/nyuuzyou/wb-products/resolve/main/basket-NN.json.zst` (302 to a CDN, followed by
  `HttpClient`), read line by line, each line parsed with `System.Text.Json`. Nothing is saved.
- **Rationale**: pure managed code, streaming, no native dependency; verified to build on .NET 10.

## R3. Photos

- **Decision**: `SkiaSharp` 4.157.0: decode WebP, resize so the long side is 800 px (Mitchell cubic), encode JPEG at
  quality 75, file `wb-<nm_id>.jpg`.
- **Rationale**: MIT licence. A probe converted a real 900×1200 card photo to 600×800 at quality 80 in 83 KB; quality 75
  keeps 100 photos near 7 MB, under the 10 MB budget with the product file.
- **Alternatives**: ImageSharp 4.x needs a Six Labors licence key (build warning, split licence); 3.1.12 has known
  high-severity advisories (NU1903). `sips` is macOS-only.

## R4. Output validation

- **Decision**: `JsonSchema.Net` 9.7.0 evaluates the output against
  `specs/001-test-shop/contracts/product-file.schema.json` before anything is written; on failure the tool exits
  non-zero and writes nothing. The tool also enforces the rules the schema cannot: unique SKUs and slugs, parents
  present, image file present.
- **Rationale**: the loader's `ProductFileValidator` is `internal` to an AOT executable, so the tool cannot call it; the
  committed subset is additionally checked by `SampleCatalogTests` with that validator.

## R5. Which dump files

- **Decision**: option `--dumps` (default `12`), a list or range such as `9-12`. With `--limit` the tool stops reading
  once it has enough candidates; without it, it reads all the given files.
- **Rationale**: `basket-12` (35 MB, ≈713 candidates in the 8 subjects) is enough for the subset; a few thousand
  products need several files, and reading all 15.8 GB by default would be wasteful. The notes' "one command, optional
  `--limit`" is kept: there is still no separate subset or raw command.

## R6. Selection, deduplication, filtering

- Subjects: Смартфоны, Ноутбуки, Планшеты, Телевизоры, Наушники, Мониторы, Колонки, Смарт-часы, each with its key
  characteristics (for example a display diagonal for screens), kept in one table in the tool. The exact option names
  are taken from real cards during implementation.
- One product per `imt_id`: the lowest `nm_id` wins (deterministic).
- Candidate order: by a stable hash of `nm_id` (not `string.GetHashCode`, which is randomized per process), so the
  subset is a spread, not the oldest products.
- Skip, with a counted reason: no card (404 on every host), no brand, no description, no photo, missing key
  characteristics.
- With `--limit N`: round-robin over subjects, and within a subject prefer brands not yet taken, so 100 gives every
  subject ≈12 products and well over 10 brands. Fetching happens only for candidates being considered.

## R7. Card host and politeness

- Card host `basket-NN.wbbasket.ru` depends on `vol = nm_id / 100000`. For an unknown `vol`, probe hosts `01`…`40` in
  order until the card is found and cache the `vol → host` map on disk.
- One request per second overall (a single `PeriodicTimer` gate), a descriptive `User-Agent`, retries with backoff on
  timeouts, 429 and 5xx (three attempts), then skip.
- Cache layout: `<cache>/cards/<nm_id>.json`, `<cache>/photos/<nm_id>.webp`, `<cache>/missing/<nm_id>` (known 404s),
  `<cache>/hosts.json`. Default cache `.cache/build-catalog/` (git-ignored). A re-run makes no request for a cached
  item.

## R8. Mapping details

- Category slugs: transliteration (ГОСТ 7.79 System B style, lowercase, non-alphanumerics to `-`, collapsed), e.g.
  `Смартфоны и аксессуары` → `smartfony-i-aksessuary`. Root subject → parent category, subject → child.
- Attributes: flatten `grouped_options`; a name that occurs in more than one group becomes `<group>: <name>`; names
  are cut to 64 characters, and a collision after cutting keeps the first value. Values are text, trimmed. If a card has
  no `grouped_options`, use `options`.
- Prices: per-subject range, from `new Random(nm_id)` (seeded `Random` uses the stable legacy algorithm), rounded to
  end in 90 rubles (e.g. 24 990). Quantity 0–50, with `quantity = 0` for ≈5%.

  | Subject | Range, ₽ |
  |---------|----------|
  | Смартфоны | 8 000 – 150 000 |
  | Ноутбуки | 30 000 – 250 000 |
  | Планшеты | 10 000 – 120 000 |
  | Телевизоры | 15 000 – 200 000 |
  | Мониторы | 10 000 – 100 000 |
  | Наушники | 1 000 – 30 000 |
  | Колонки | 1 500 – 40 000 |
  | Смарт-часы | 3 000 – 60 000 |

## R9. Tests over the real subset

- `SampleCatalogTests`: valid with the loader's validator; exactly 100 products; ≥5 child categories; ≥10 brands; in
  and out of stock; file plus images under 10 MB; `generated` is false and `source` names `nyuuzyou/wb-products`.
- `SampleDemoTests`: the product to check (name, price, brand, an attribute, photo path) is read from the file through
  a helper in `ShopTest`, not hard-coded. It checks the product page rather than a category page, which may paginate.
- `TestCatalog` and `ShopTest.SampleImage` use `Support/photo.jpg`, so loader tests do not depend on the demo data.
- `tools/smoke-test.sh`: the brand to search is read from `data/sample-catalog.json` with `grep`/`sed` (no new
  dependency); the product-count threshold becomes 100.

## R10. Full set

- `--archive` packs the product file and `images/` into `catalog.tar.gz` with `System.Formats.Tar` and `GZipStream`.
  Uploading it to S3 is manual and outside this feature.
