# Implementation Plan: Shared Shop Client Library

**Branch**: `002-shared-shop-client` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-shared-shop-client/spec.md`

## Summary

Move the loader's shop-facing code (`ICatalogClient`, `OpenCartCatalogClient`, its JSON request and response types and
failure types) into a new .NET class library, `src/ShopClient`. The library exposes two shop-neutral contracts (general
shop, catalog loading), its own shop-neutral category and product types, and a factory that creates a client from a shop
type. The loader references the library, maps its file records to the library's types, and gets a new optional `--shop`
flag that defaults to OpenCart. Behavior, packaging (Native AOT, one image) and the OpenCart extension do not change.
See [research.md](research.md) for decisions and [contracts/shop-client-api.md](contracts/shop-client-api.md) for the
library's public surface.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: none new (retries are our own helper, not Polly; see research R9). The library uses `System.Text.Json` with source generation and `HttpClient`; the loader keeps `System.CommandLine`.

**Storage**: N/A

**Testing**: xUnit. Client unit tests move to a new `tests/ShopClient.UnitTests`; client behavior against the real shop is covered by a new integration test in the existing `ShopLoader.IntegrationTests` (Testcontainers, real OpenCart and MariaDB, no stand-ins).

**Target Platform**: library consumed by a Native AOT console app on Docker/Linux, and by tests on the normal runtime

**Project Type**: class library plus its consumer (the loader CLI)

**Performance Goals**: no change from the loader today (first start with seeding under 10 minutes)

**Constraints**: library must be AOT and trim compatible (`IsAotCompatible`, analyzers on, reflection-free JSON); no dependency from the library to the loader; API only, no database access; loader behavior unchanged apart from the optional `--shop` flag

**Scale/Scope**: about 10 source files move, two contracts, one implementation, one factory

## Constitution Check

*Gate before Phase 0; re-checked after Phase 1.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. .NET-first backend | Pass | All .NET; no Python, no new PHP. |
| II. Integrate, never replace | Pass | This feature is what the principle asks for: shop-specific code confined to one implementation behind shop-neutral contracts, chosen by a factory so consumers need no change for a new shop. The test-shop feature's deviation (loader calling the extension directly) is removed for the client code. Shop provisioning with SQL is untouched. |
| III. Grounded and safe assistant | N/A | No assistant behavior. |
| IV. Human in the loop | N/A | Not touched. |
| V. Test-first | Pass | Unit tests for factory and failure mapping, one integration test through the contracts against the real shop; no stand-ins; shared helpers stay in `UnitTest` and `ShopTest`; client scenarios covered once (see research.md R6). |
| VI. Language policy | Pass | English docs and code. |
| VII. Incremental simplicity | Pass | Only the four operations the loader uses; no read operations, no second shop, no package publishing. |
| VIII. Infrastructure as code | Pass | The loader Dockerfile is updated to copy the library; nothing manual. |
| IX. Containers with dev overrides | Pass | Compose files unchanged; the loader image still builds from the repository root. |

Post-design re-check: unchanged, no violations.

## Project Structure

### Documentation (this feature)

```text
specs/002-shared-shop-client/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── shop-client-api.md
└── tasks.md             # created by /speckit-tasks
```

### Source Code (repository root)

```text
src/ShopClient/                       # NEW class library (AOT compatible)
├── ShopClient.csproj
├── README.md                         # purpose, contracts, how to add a shop
├── IShopClient.cs                    # general contract: readiness
├── ICatalogLoadingClient.cs          # loading contract: category/product upsert, image upload
├── CatalogCategory.cs                # shop-neutral types (+ UpsertResult)
├── CatalogProduct.cs
├── Exceptions.cs                     # CatalogException, ShopUnavailableException, UnsupportedShopException
├── ShopType.cs                       # supported shop types and parsing
├── ShopConnection.cs                 # url, api key, request timeout
├── ShopClientFactory.cs              # creates a client for either contract from a shop type
└── OpenCart/                         # internal; the only code that knows OpenCart
    ├── OpenCartClient.cs             # moved and renamed from ShopLoader/OpenCart/OpenCartCatalogClient.cs
    └── OpenCartJsonContext.cs        # moved from ShopLoader/Json/LoaderJsonContext.cs
src/ShopLoader/                       # uses the library; no OpenCart code left
├── Cli.cs                            # + optional --shop flag, uses the factory
├── Loader.cs                         # uses ICatalogLoadingClient and maps file records to catalog types
├── Models/ProductFile.cs             # stays: the product file format is a loader concern
└── Dockerfile                        # also copies src/ShopClient
tests/ShopClient.UnitTests/           # NEW: moved OpenCartCatalogClientTests, factory tests
tests/ShopLoader.UnitTests/           # loses the client test, keeps the rest; gains --shop tests in CliTests
tests/ShopLoader.IntegrationTests/    # + ShopClientTests (library only, through the factory)
```

**Structure Decision**: The library lives in `src/ShopClient` and is consumed by project reference. Its OpenCart code is
`internal`, so consumers can only reach it through the factory; `InternalsVisibleTo` gives the library's unit tests
access. Integration tests stay in the one project that owns the container stack (`ShopStack`), to avoid duplicating
container setup; this narrows spec FR-012, which is amended to match.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | | |
