# Implementation Plan: Real Demo Catalog

**Branch**: `003-real-demo-catalog` | **Date**: 2026-11-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-real-demo-catalog/spec.md`

## Summary

A `dotnet run` file app, `tools/build-catalog/build-catalog.cs`, streams the Wildberries dump from Hugging Face,
selects and deduplicates electronics products, fetches their `card.json` and main photo at one request per second into
an on-disk cache, filters noise, maps them to the spec 001 product file with seeded prices and stock, converts photos
to JPEG, validates against the schema and writes the output. Its 100-product output replaces `data/sample-catalog.json`
and `data/images/`; tests and the smoke test read their expectations from the file; `tools/generate-sample` is removed.
Decisions are in [research.md](research.md), the command line in [contracts/build-catalog-cli.md](contracts/build-catalog-cli.md).

## Technical Context

**Language/Version**: C# on .NET 10, file-based app (`dotnet run file.cs`), like the removed `generate.cs`

**Primary Dependencies**: `ZstdSharp.Port` (zstd stream), `SkiaSharp` (WebP decode, resize, JPEG encode),
`JsonSchema.Net` (output validation); BCL `HttpClient` and `System.Text.Json`. See research R2–R4.

**Storage**: files only: cache directory (cards, photos, card-host map) and the output directory

**Testing**: no tests for the tool (spec assumption). Existing xUnit suites cover the committed subset:
`ShopLoader.UnitTests/SampleCatalogTests` (subset rules) and `ShopLoader.IntegrationTests/SampleDemoTests` (real shop
via Testcontainers)

**Target Platform**: developer machine (macOS/Linux) with the .NET 10 SDK; not run in CI or containers

**Project Type**: one-off CLI tool plus data and test updates

**Performance Goals**: 100-product run in under 15 minutes with a cold cache (≈2 requests per product at 1 req/s plus
streaming one 35 MB dump file)

**Constraints**: ≤1 request per second to Wildberries; deterministic output; subset under 10 MB; product file format
and loader unchanged

**Scale/Scope**: subset 100 products; full set a few thousand

## Constitution Check

*Gate before Phase 0; re-checked after Phase 1.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. .NET-first backend | Pass | The tool is .NET; Python is not used. |
| II. Integrate, never replace | Pass | The catalog still reaches the shop only through the existing loader and API. |
| III. Grounded and safe assistant | N/A | No assistant behavior. Real data makes later grounding tests more meaningful. |
| IV. Human in the loop | N/A | Not touched. |
| V. Test-first | Pass | No tests for a run-once tool (it would test the dump, not our code); its output is schema-validated and the committed subset is covered by existing tests, which stop naming invented products. No stand-ins. |
| VI. Language policy | Pass | Code and docs in English; catalog content in Russian. |
| VII. Incremental simplicity | Pass | One command, one optional `--limit` plus `--dumps` (R5); no upload to S3, no scheduled refresh. |
| VIII. Infrastructure as code | Pass | No servers involved. |
| IX. Containers | Pass | The environment and its single command are unchanged; the tool is a developer utility outside the runtime, like `generate.cs` was. |
| Tech constraint: test data | Pass | One-time snapshot of a public catalog (CC0 dump + public card files), loaded through the API, non-commercial (see `data/README.md`). |

Post-design re-check: unchanged, no violations.

## Project Structure

### Documentation (this feature)

```text
specs/003-real-demo-catalog/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── build-catalog-cli.md
└── tasks.md             # created by /speckit-tasks
```

### Source Code (repository root)

```text
tools/build-catalog/build-catalog.cs   # NEW: the tool
tools/generate-sample/                  # REMOVED
tools/smoke-test.sh                     # search by a brand read from the file
data/sample-catalog.json                # REPLACED by the 100-product subset
data/images/                            # REPLACED: wb-<nm_id>.jpg
data/README.md                          # source, date, terms, how to rebuild
tests/ShopLoader.UnitTests/SampleCatalogTests.cs              # subset rules (FR-009, FR-011, source)
tests/ShopLoader.IntegrationTests/SampleDemoTests.cs          # expectations from the file
tests/ShopLoader.IntegrationTests/Support/ShopTest.cs         # SampleImage -> own test photo
tests/ShopLoader.IntegrationTests/Support/TestCatalog.cs      # own test photo
tests/ShopLoader.IntegrationTests/Support/photo.jpg           # NEW test photo
.gitignore                              # the tool's cache and output directories
README.md                               # 001 row and shop description no longer say "generated"
```

**Structure Decision**: a single-file tool under `tools/`, as the repository already does for one-off generators; no
new project in the solution.
