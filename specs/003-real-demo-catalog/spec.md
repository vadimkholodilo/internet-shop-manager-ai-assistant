# Feature Specification: Real Demo Catalog

**Feature Branch**: `003-real-demo-catalog`

**Created**: 2026-11-21

**Status**: Draft

**Input**: User description: "Real demo catalog from the Wildberries dataset. Replace the generated sample catalog (data/sample-catalog.json and data/images/, produced by tools/generate-sample) with a realistic Russian electronics catalog built from the Hugging Face dump nyuuzyou/wb-products (CC0) as the index and Wildberries' public static card.json files and photos for details. Product file format from spec 001 stays unchanged and is loaded by the existing loader with no changes. All findings and decisions are in specs/003-real-demo-catalog/source-notes.md: follow it. Keep the spec short."

## Scope

The demo shop is seeded with about 30 invented products. The AI assistant needs a realistic catalog to search,
compare and recommend from: real Russian product names, brands, descriptions, characteristics and photos. This
feature adds a one-off tool that builds such a catalog from public Wildberries data and replaces the invented sample
with a ~100-product real subset. Research behind the choice of source is in [source-notes.md](source-notes.md).

**In scope**: the catalog build tool; the committed ~100-product subset and its photos; adapting the tests, the smoke
test and `data/README.md` to the real data; removing the invented-catalog generator.

**Out of scope**: any change to the product file format or the loader; hosting or loading the full set (it is
produced, then stored outside GitHub by hand); real prices or stock; scraping any site behind bot protection.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build a real catalog with one command (Priority: P1)

A developer runs one command, optionally with a product limit. The tool streams the public dump, keeps products of
the chosen electronics categories, fetches each product's card and main photo, drops noise, and writes a product file
plus an `images/` directory that the existing loader accepts unchanged.

**Why this priority**: Everything else depends on having the catalog.

**Independent Test**: Run the tool with a limit of 100 into an empty directory, validate the file against the product
file schema, and load it into the test shop with the existing loader with no rejected records.

**Acceptance Scenarios**:

1. **Given** no cache, **When** the tool runs with `--limit 100`, **Then** it writes a schema-valid product file with
   100 products and one photo per product.
2. **Given** a previous run was interrupted, **When** the tool runs again, **Then** it does not download cards or
   photos it already has and finishes the job.
3. **Given** the same inputs, **When** the tool runs twice, **Then** the two outputs are identical (products, prices,
   stock).
4. **Given** no limit, **When** the tool runs, **Then** it produces the full set (a few thousand products) and can pack
   it, with its photos, into one archive.

---

### User Story 2 - The demo shop shows the real catalog (Priority: P1)

The repository's sample catalog is the real ~100-product subset, so `docker-compose up -d` seeds the shop with real
products, and all tests pass against it.

**Why this priority**: This is what the assistant feature and blog readers will see.

**Independent Test**: Start the environment from a clean clone; the storefront shows real products with photos in
several categories, and the full PR check passes.

**Acceptance Scenarios**:

1. **Given** a clean clone, **When** the environment starts, **Then** the shop contains the subset's categories and
   products with photos, prices and stock.
2. **Given** the committed subset, **When** the tests run, **Then** no test refers to an invented product, brand or
   SKU; expectations come from the file.
3. **Given** the repository, **When** `data/README.md` is read, **Then** it states the source, collection date,
   licence of the dump, non-commercial demo use, that texts and photos belong to their owners, and that content is
   removed on request.

### Edge Cases

- A product from the dump no longer has a card or photo: skip it and continue.
- A card is an accessory filed under a main category (a TV remote under TVs): skip it because it lacks the category's
  key characteristics.
- A card has no brand: skip it.
- The card host for a product is unknown: probe the hosts once per volume and remember the answer.
- A characteristic name appears in several groups, or is longer than the shop allows: keep both, disambiguated and
  within the limit.
- A temporary network failure or rate limit: retry politely; a product that still fails is skipped and reported.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tool MUST read the dump as a stream, without keeping it on disk, and select products whose subject
  is one of a configured list (initially Смартфоны, Ноутбуки, Планшеты, Телевизоры, Наушники, Мониторы, Колонки,
  Смарт-часы).
- **FR-002**: The tool MUST keep one product per model (`imt_id`), dropping colour variants.
- **FR-003**: The tool MUST fetch each card and main photo at no more than a polite fixed rate (about one request per
  second by default) and cache them on disk; a re-run MUST reuse the cache.
- **FR-004**: The tool MUST skip products without a brand, without a photo, or missing their category's key
  characteristics, and report how many were skipped and why.
- **FR-005**: The tool MUST map products to the product file as follows: SKU `WB-{nm_id}`, name, brand, description
  and characteristics from the card; categories from the card's root subject (parent) and subject (child) with
  transliterated slugs; characteristic names unique and at most 64 characters; values kept as text.
- **FR-006**: The tool MUST generate a price within a plausible range per category and a quantity from 0 to 50 with
  about 5% out of stock, derived deterministically from the product ID.
- **FR-007**: The tool MUST convert photos to JPEG with the long side at most 800 px, named after the SKU.
- **FR-008**: The tool MUST validate the output against the product file schema before writing it, and mark the file
  as not generated with a `source` naming the dump and the card files.
- **FR-009**: With a limit, the tool MUST spread the selection over categories and brands: for 100 products, at least
  5 categories and at least 10 brands, with both in-stock and out-of-stock products.
- **FR-010**: Without a limit, the tool MUST be able to pack the product file and photos into one archive.
- **FR-011**: The committed subset MUST replace `data/sample-catalog.json` and `data/images/`, stay under 10 MB, and
  the invented-catalog generator MUST be removed.
- **FR-012**: Tests and the smoke test MUST take product, brand and SKU expectations from the committed file; tests
  that need a fixed photo MUST use their own test photo. A test MUST check the subset rules of FR-009 and FR-011 and
  the source.

### Key Entities

- **Dump record**: one product in the public index (model ID, product ID, name, subject, root subject, brand).
- **Card**: a product's public details (description, grouped characteristics, brand, photo count).
- **Product file**: the existing format from spec 001 (categories, products, images).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The committed subset loads into the test shop with zero rejected records.
- **SC-002**: The subset has 100 products in at least 5 categories and from at least 10 brands, including both in-stock
  and out-of-stock products, and takes under 10 MB.
- **SC-003**: Every product in the subset has a real name, brand, description, at least 5 characteristics and a photo.
- **SC-004**: A re-run with a warm cache makes no network requests for cached products and gives identical output.
- **SC-005**: The full PR check passes with the real subset and no test names an invented product.

## Assumptions

- The dump (collected about 2023) and the card files stay publicly available; checks on 2026-11-20 found 24 of 24
  sampled electronics cards still present.
- The dump is CC0; card texts and photos belong to Wildberries and its sellers and are used only for a
  non-commercial demo, with removal on request.
- Seller brand variants (e.g. `GALAXY` for Samsung) and typos in values are acceptable as they are.
- The tool runs once on a developer machine; it has no test suite of its own beyond schema validation of its output
  and the existing tests over the committed subset.
- Which stack the tool uses is decided in the plan (Principle I: .NET unless Python is justified).
