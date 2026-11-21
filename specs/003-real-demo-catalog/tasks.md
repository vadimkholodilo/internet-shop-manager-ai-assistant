---

description: "Task list for the Real Demo Catalog"
---

# Tasks: Real Demo Catalog

**Input**: Design documents from `/specs/003-real-demo-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/build-catalog-cli.md, quickstart.md

**Tests**: No tests for the tool (spec assumption, plan Principle V row). Existing tests are adapted to the real subset
(FR-012, research R9), following the constitution: shared helpers in `ShopTest`, no stand-ins.

**Organization**: US1 builds the tool, US2 swaps the committed catalog. Paths are relative to the repository root.
All tool code lives in one file, `tools/build-catalog/build-catalog.cs`, so US1 tasks are sequential.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Create `tools/build-catalog/build-catalog.cs` with a header comment (purpose, how to run), `#:property PublishAot=false`, `#:package ZstdSharp.Port@0.8.8`, `#:package SkiaSharp@4.157.0`, `#:package JsonSchema.Net@9.7.0`, and option parsing for `--limit`, `--dumps` (default `12`, numbers and ranges), `--out` (default `out/catalog`), `--cache` (default `.cache/build-catalog`), `--archive`; exit code 2 on invalid arguments (contracts/build-catalog-cli.md)
- [X] T002 [P] Add `/out/` and `/.cache/` to `.gitignore`

## Phase 2: User Story 1 - Build a real catalog with one command (P1) 🎯 MVP

**Goal**: one command writes a schema-valid product file plus `images/` from the dump and card files.

**Independent Test**: quickstart steps 1–2: `--limit 100` gives a valid file with 100 products, a re-run makes no
Wildberries requests and gives identical output.

- [X] T003 [US1] Dump reader in `tools/build-catalog/build-catalog.cs`: stream `https://huggingface.co/datasets/nyuuzyou/wb-products/resolve/main/basket-NN.json.zst` through `ZstdSharp.DecompressionStream`, parse lines, keep records whose `subj_name` is in the subject table (Смартфоны, Ноутбуки, Планшеты, Телевизоры, Наушники, Мониторы, Колонки, Смарт-часы), one per `imt_id` (lowest `nm_id`); with `--limit`, stop reading once there are enough candidates; exit code 3 if a dump cannot be read (R2, R5, R6)
- [X] T004 [US1] Polite HTTP client in `tools/build-catalog/build-catalog.cs`: one request per second overall, descriptive `User-Agent`, three attempts with backoff on timeouts, 429 and 5xx; card host resolution per `vol = nm_id / 100000` by probing `basket-01`…`basket-40` and caching `<cache>/hosts.json`; cache cards in `<cache>/cards/<nm_id>.json`, photos (`images/big/1.webp`) in `<cache>/photos/<nm_id>.webp`, known 404s in `<cache>/missing/<nm_id>`; count network requests vs cache hits (R7)
- [X] T005 [US1] Fetch a few real cards per subject with T004 and fill the key-characteristics column of the subject table (e.g. a display diagonal for Смартфоны, Ноутбуки, Планшеты, Телевизоры, Мониторы) plus the price ranges from research R8 in `tools/build-catalog/build-catalog.cs`
- [X] T006 [US1] Filtering and selection in `tools/build-catalog/build-catalog.cs`: order candidates by a stable hash of `nm_id`; skip with a counted reason when there is no card, no brand, no description, no photo, or a missing key characteristic; with `--limit N`, round-robin over subjects and prefer brands not yet taken in a subject, fetching only candidates being considered (R6)
- [X] T007 [US1] Mapping in `tools/build-catalog/build-catalog.cs` per data-model.md: `sku` `WB-{nm_id}`, `name` `imt_name`, `brand` `selling.brand_name`, `description`, categories from `subj_root_name` (parent) and `subj_name` (child) with transliterated slugs matching `^[a-z0-9-]+$`; attributes flattened from `grouped_options` (fallback `options`), a name in more than one group becomes `<group>: <name>`, names cut to 64 characters, first value wins on collision; `generated: false` and the `source` text from data-model.md; products sorted by category slug then SKU (R8)
- [X] T008 [US1] Prices and stock in `tools/build-catalog/build-catalog.cs`: `new Random(nm_id)`, price in the subject's range ending in 90 rubles, quantity 0–50 with ≈5% set to 0 (R8)
- [X] T009 [US1] Photos in `tools/build-catalog/build-catalog.cs`: SkiaSharp decode WebP, resize so the long side is at most 800 px (Mitchell), JPEG quality 75, written as `images/wb-<nm_id>.jpg` (R3)
- [X] T010 [US1] Validation and output in `tools/build-catalog/build-catalog.cs`: evaluate against `specs/001-test-shop/contracts/product-file.schema.json` with JsonSchema.Net, check unique SKUs and slugs, parents and images present, and with `--limit 100` the subset rules (100 products, ≥5 child categories, ≥10 brands, in and out of stock); on failure exit 1 and write nothing; otherwise replace `<out>/catalog.json` and `<out>/images/` and print the summary from the contract (R4)
- [X] T011 [US1] `--archive` in `tools/build-catalog/build-catalog.cs`: write `<out>/catalog.tar.gz` with `System.Formats.Tar` and `GZipStream` (R10)
- [X] T012 [US1] Run quickstart steps 1–2 (`--limit 100 --out out/subset`, then a re-run and `cmp`); fix until the run succeeds, the subset is under 10 MB and the re-run makes no Wildberries requests

**Checkpoint**: the tool produces a valid, deterministic 100-product subset.

## Phase 3: User Story 2 - The demo shop shows the real catalog (P1)

**Goal**: the committed sample is the real subset and every test and script works with it.

**Independent Test**: quickstart step 3: `dotnet test` and `tools/smoke-test.sh` pass; no test or script names Norda or
`PH-001`.

- [X] T013 [US2] Replace `data/sample-catalog.json` and `data/images/` with `out/subset/catalog.json` and `out/subset/images/` (contract "Updating the committed subset")
- [X] T014 [P] [US2] Add a small JPEG test photo `tests/ShopLoader.IntegrationTests/Support/photo.jpg` (copy of one subset photo is fine), copied to the output by the test project; point `SampleImage` in `tests/ShopLoader.IntegrationTests/Support/ShopTest.cs` and the photo copy in `tests/ShopLoader.IntegrationTests/Support/TestCatalog.cs` at it
- [X] T015 [P] [US2] Rewrite `tests/ShopLoader.UnitTests/SampleCatalogTests.cs` for the subset rules: valid with `ProductFileValidator`; exactly 100 products; ≥5 child categories; ≥10 brands; in and out of stock; file plus images under 10 MB; `"generated": false` and `source` contains `nyuuzyou/wb-products`; keep the Cyrillic check (R9)
- [X] T016 [US2] Add a helper to `tests/ShopLoader.IntegrationTests/Support/ShopTest.cs` that reads one sample product (SKU, name, brand, price, an attribute name, image) from `data/sample-catalog.json`; update `tests/ShopLoader.IntegrationTests/SampleDemoTests.cs` to use it, check the product page instead of the category page, and replace the `>= 20` threshold with the product count from the file (R9)
- [X] T017 [P] [US2] Update `tools/smoke-test.sh`: read the search brand from `data/sample-catalog.json` with `grep`/`sed` instead of `Norda`, and require at least 100 products (R9)
- [X] T018 [P] [US2] Rewrite `data/README.md`: source (Hugging Face `nyuuzyou/wb-products`, CC0, plus Wildberries public card files and photos), fetch date, non-commercial demo use, texts and photos belong to their owners, removal on request, how to rebuild with `tools/build-catalog/build-catalog.cs`; update the format example to a real-style record
- [X] T019 [P] [US2] Remove `tools/generate-sample/`; update `README.md` (001 row and shop description no longer say "generated") and the "generated" wording in `docker-compose.yml` comments and `tests/ShopLoader.IntegrationTests/SampleDemoTests.cs` doc comment if present
- [X] T020 [US2] Run `dotnet test` and `tools/smoke-test.sh`; `grep -rE "Norda|PH-001|generate-sample" --exclude-dir=specs --exclude-dir=.git .` finds nothing outside the notes

**Checkpoint**: clean clone seeds the shop with the real catalog; the PR check passes.

## Phase 4: Polish

- [X] T021 Move the repository-root `catalog-dataset-notes.md` to [source-notes.md](source-notes.md), record implementation findings in research.md R11, and update the project memory note that points to it
- [ ] T022 Optional: run quickstart step 4 (full set with `--dumps 9-12 --archive`) and hand the archive to the user for S3

## Dependencies

- T001 → T003 → T004 → T005 → T006 → T007 → T008 → T009 → T010 → T011 → T012 (one file)
- US2 needs T012 (the subset). T014, T015, T017, T018, T019 run in parallel after T013; T016 after T014 (same file);
  T020 last.
- Polish after US2.

## Parallel Example: User Story 2

```text
After T013: T014 (test photo), T015 (SampleCatalogTests), T017 (smoke test), T018 (data/README.md), T019 (cleanup)
```

## Implementation Strategy

MVP is US1: the tool and a verified subset in `out/subset`, without touching the repository's data. US2 then swaps the
data and the tests in one commit-sized step, so the PR check stays green at every commit.
