# Implementation Plan: Test Electronics Shop (Local Demo)

**Branch**: `001-test-shop` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-test-shop/spec.md`

## Summary

Run OpenCart and its database in containers started by one `docker-compose up -d`, with a loader that
fills the shop from a JSON file and a generated Russian sample catalog. OpenCart has no core API for
creating catalog items (see [research.md](research.md)), so the gap is closed by a small in-repo OpenCart
extension that exposes an authenticated catalog upsert endpoint using OpenCart's own models. The loader is
a .NET 10 console app, published with Native AOT, that talks only to that endpoint over HTTP. Integration tests use Testcontainers with
the same compose files and real containers.

## Technical Context

**Language/Version**: C# on .NET 10 (loader, tests); PHP (OpenCart extension, required by the platform)

**Primary Dependencies**: OpenCart 4.x (built from a pinned release), MariaDB, Testcontainers for .NET, xUnit. The loader uses only AOT-compatible libraries: `System.Text.Json` with source generation, `HttpClient`, and hand-written argument parsing and validation (no reflection-based libraries).

**Storage**: MariaDB, owned by OpenCart; the loader never connects to it

**Testing**: xUnit unit tests for parsing, validation and mapping; Testcontainers integration tests against
real OpenCart and MariaDB containers

**Target Platform**: Docker with the Compose plugin on a developer or reader machine, and GitHub Actions
`ubuntu-latest`

**Project Type**: CLI tool plus infrastructure definitions (compose, image build). The loader is published with Native AOT (`PublishAot`) and runs in a minimal runtime-deps image; test projects run on the normal runtime because xUnit and Testcontainers rely on reflection.

**Performance Goals**: First start including seeding in under 10 minutes; storefront list page in under 5 seconds

**Constraints**: Single command start, idempotent re-runs, no direct database access, no committed secrets

**Scale/Scope**: About 3 categories and 20-40 generated products

## Constitution Check

*Gate before Phase 0; re-checked after Phase 1.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. .NET-first backend | Pass | Loader and tests in .NET 10, loader as a Native AOT binary. PHP only for the OpenCart extension, which runs inside OpenCart; no Python. |
| II. Integrate, never replace | Pass with deviation | Uses OpenCart, no database access. The loader calls the extension endpoint directly instead of a connector; see Complexity Tracking. |
| III. Grounded and safe assistant | N/A | No assistant behaviour in this slice. |
| IV. Human in the loop | N/A | Not touched. |
| V. Test-first | Pass | Unit and Testcontainers integration tests, run in the `tests` workflow. |
| VI. Language policy | Pass | English docs and code; the sample catalog content is Russian. |
| VII. Incremental simplicity | Pass | Public address, connector, orders and reset deferred. |
| VIII. Infrastructure as code | Pass | Everything defined in compose files and Dockerfiles; no manual steps. |
| IX. Containers with dev overrides | Pass | `docker-compose.yml` plus `docker-compose.dev.yml` exposing shop and database ports. |

Post-design re-check: unchanged; the only deviation is the one below.

## Project Structure

### Documentation (this feature)

```text
specs/001-test-shop/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── product-file.schema.json
│   └── catalog-endpoint.md
└── tasks.md             # created by /speckit-tasks
```

### Source Code (repository root)

```text
docker-compose.yml            # opencart, mariadb, loader (default, no db port exposed)
docker-compose.dev.yml        # dev override: exposes shop and database ports
.env.example                  # non-secret local defaults
shop/
├── Dockerfile                # OpenCart built from a pinned release, plus our extension
├── entrypoint.sh             # unattended install and extension activation, idempotent
└── extension/catalog_api/    # PHP extension: authenticated catalog upsert endpoint
data/
├── sample-catalog.json       # generated Russian sample catalog
└── images/                   # generated placeholder photos
src/ShopLoader/               # .NET console loader (reads JSON, calls the endpoint)
tests/ShopLoader.UnitTests/
tests/ShopLoader.IntegrationTests/   # Testcontainers, real OpenCart and MariaDB
tools/generate-sample/        # script that generates the sample catalog
```

**Structure Decision**: One .NET solution at the root with the loader and two test projects. Shop
infrastructure lives under `shop/`, so the loader's OpenCart-specific HTTP code sits in one class
(`OpenCartCatalogClient`) that can move into the connector in the next feature.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Loader calls the OpenCart extension directly, not via a connector (Principle II) | The connector is deferred to the next feature to keep this slice small | Building the connector now would add the contract design to a slice meant to prove only the one-command demo. The OpenCart-specific code is isolated in one class so it moves into the connector later. |
| Custom PHP extension inside OpenCart | OpenCart core has no catalog write API | A third-party API extension is unverified and may be paid; driving the admin UI with form posts is fragile; direct database writes are forbidden. |
