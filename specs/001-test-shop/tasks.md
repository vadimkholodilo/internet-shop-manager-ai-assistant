---

description: "Task list for the Test Electronics Shop (Local Demo)"
---

# Tasks: Test Electronics Shop (Local Demo)

**Input**: Design documents from `/specs/001-test-shop/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Requested (FR-014, FR-015, User Story 5). Unit tests cover parsing, validation and mapping; Testcontainers integration tests run against real OpenCart and MariaDB containers.

**Organization**: Tasks are grouped by user story. Because User Story 1 (one-command demo) needs the loader (US2) and the sample catalog (US3), the phases run in the order Foundational, US2, US3, US1, US4, US5. Each phase is still independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story the task belongs to (US1 to US5)

## Path Conventions

Paths follow plan.md: `shop/` (image and extension), `src/ShopLoader/`, `tests/ShopLoader.UnitTests/`, `tests/ShopLoader.IntegrationTests/`, `data/`, `tools/generate-sample/`, compose files at the repository root.

---

## Phase 1: Setup

- [X] T001 Create the .NET 10 solution `InternetShop.slnx` with projects `src/ShopLoader/ShopLoader.csproj` (console, `PublishAot` true, `InvariantGlobalization` true, IL trim and AOT warnings as errors), `tests/ShopLoader.UnitTests/ShopLoader.UnitTests.csproj` and `tests/ShopLoader.IntegrationTests/ShopLoader.IntegrationTests.csproj` (xUnit, Testcontainers); the test projects do not use AOT
- [X] T002 [P] Create `.env.example` with non-secret local defaults only: `SHOP_PORT=8080`, `SHOP_DB_PORT=3306`, `SHOP_ADMIN_USER`, `SHOP_ADMIN_PASSWORD`, `SHOP_DB_PASSWORD`, `SHOP_API_KEY`, and add `.env` to `.gitignore`
- [X] T003 [P] Add `.editorconfig` and `Directory.Build.props` at the repository root with nullable enabled, `TreatWarningsAsErrors` true and a pinned .NET 10 SDK in `global.json`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: A running OpenCart with Russian language, rubles and our extension installed, plus the extension's authentication. Nothing else works without it.

- [X] T004 Resolve the "Verify" items in `specs/001-test-shop/research.md` R1-R4 by experiment (OpenCart 4.1.0.4 release and compatible PHP version, CLI installer flags, how the extension loads admin models and registers an API-key-protected route, Russian language pack source) and record the outcomes in research.md and the route in `specs/001-test-shop/contracts/catalog-endpoint.md`
- [X] T005 Create `shop/Dockerfile` building OpenCart 4.1.0.4 on the chosen `php:*-apache` base with the required PHP extensions, copying `shop/extension/catalog_api/` into place and `shop/entrypoint.sh`
- [X] T006 Create `shop/entrypoint.sh` and `shop/provision.sh`: wait for MariaDB, run the OpenCart CLI installer once (guarded by a marker such as an existing `config.php`), set Russian as the default language and RUB as the default currency, install and enable the `catalog_api` extension, and create the API key from `SHOP_API_KEY`; every step safe to re-run
- [X] T007 Create the base `docker-compose.yml` with services `db` (MariaDB, healthcheck, named volume, no host port) and `shop` (built from `shop/`, healthcheck on the storefront and the extension, port `${SHOP_PORT}`), reading values from `.env`
- [X] T008 Implement bearer-token authentication for the extension in `shop/catalog_api/controller/api.php` so missing or wrong keys return `401` and change nothing, per `contracts/catalog-endpoint.md`
- [X] T009 Verify manually with `docker-compose up -d` that the storefront opens in Russian with rubles and that an unauthenticated call to the extension returns `401`; record the result in the PR description

**Checkpoint**: OpenCart installs unattended and the extension answers `401` without a key.

---

## Phase 3: User Story 2 - Load products from a JSON file through the shop API (Priority: P1)

**Goal**: A loader reads the product file and fills the shop through the extension endpoint only; re-runs create no duplicates.

**Independent Test**: Start an empty shop, run the loader on a small fixture file, count items; run it again and verify counts are unchanged and a changed price is applied.

### Tests for User Story 2

- [X] T010 [P] [US2] Unit tests for product file validation in `tests/ShopLoader.UnitTests/ProductFileValidatorTests.cs` covering: file missing, invalid JSON, wrong format, duplicate `sku` keeps the first, missing or non-positive `price`, missing `image` file, unknown `category` slug, Cyrillic and very long values
- [X] T011 [P] [US2] Unit tests for request mapping and the load report in `tests/ShopLoader.UnitTests/LoadReportTests.cs` (a failed record is reported with its `sku` and the others continue)
- [X] T012 [P] [US2] Integration tests in `tests/ShopLoader.IntegrationTests/LoaderTests.cs` and `CatalogApiTests.cs` against real containers: first load creates everything, second load creates 0 duplicates, a changed price updates only that product, a malformed record is reported and the rest load, Cyrillic round-trips (uses a Testcontainers fixture from T013)
- [X] T013 [US2] Testcontainers fixture in `tests/ShopLoader.IntegrationTests/ShopStack.cs` that builds the repository's own `shop/Dockerfile` and `src/ShopLoader/Dockerfile` and starts real MariaDB and OpenCart containers with the same environment as `docker-compose.yml` (the Compose module cannot build local contexts, see research R7), exposing the shop URL and API key and removing containers afterwards; tests use unique SKUs so they do not depend on order

### Implementation for User Story 2

- [X] T014 [P] [US2] Models in `src/ShopLoader/Models/` for the product file, category and product with the rules from `data-model.md` quoted in code comments: `sku` unique and stable; `price` required, greater than 0, in rubles; `quantity` integer 0 or more; `image` path relative to `data/images/` and the file must exist; `category` is the slug of an existing category; `slug` matches `^[a-z0-9-]+$`
- [X] T015 [P] [US2] Source-generated JSON context in `src/ShopLoader/Json/LoaderJsonContext.cs` (`System.Text.Json`, no reflection) for the product file and endpoint representations
- [X] T016 [US2] Product file validator in `src/ShopLoader/Validation/ProductFileValidator.cs`, hand-written (no schema library), implementing `contracts/product-file.schema.json`; validates the whole file before any request is made
- [X] T017 [US2] Extension endpoints in `shop/catalog_api/controller/api.php` for upsert, get, get-by-id and list of categories per `contracts/catalog-endpoint.md`, keyed by `slug`, returning `id`, `slug`, `parent_id`, `parent_slug` and `created`
- [X] T018 [US2] Extension endpoints in `shop/catalog_api/controller/api.php` for upsert, get, get-by-id and list of products and the image upload per `contracts/catalog-endpoint.md`, keyed by `sku`, using OpenCart's own models only (no direct SQL), returning `id`, `category_id`, `category_slug`, `image_url` and `created`; ids are never accepted in requests
- [X] T019 [US2] `OpenCartCatalogClient` in `src/ShopLoader/OpenCart/OpenCartCatalogClient.cs`: the only class that knows about OpenCart; `HttpClient` calls to the extension with the bearer token, retries with a bounded timeout while the shop is not ready, clear failure afterwards (so it can move into the connector later)
- [X] T020 [US2] Loader pipeline in `src/ShopLoader/Loader.cs`: validate, upsert categories (parents first), upsert products, upload images, collect failures into a report and continue after a failed record; exit code non-zero if any record failed
- [X] T021 [US2] CLI entry point in `src/ShopLoader/Program.cs` with hand-written argument parsing for `--file`, `--url`, `--api-key` (also read from environment), `--images-dir`; clear messages and a non-zero exit before any change when the file is missing, not valid JSON or the wrong format
- [X] T022 [US2] Make `dotnet publish -c Release` of `src/ShopLoader` succeed with Native AOT with no trim or AOT warnings, and add `src/ShopLoader/Dockerfile` (SDK image with clang and zlib to build, `runtime-deps` image to run)

**Checkpoint**: Loader unit tests pass; integration tests T012 pass against real containers.

---

## Phase 4: User Story 3 - Generated sample catalog (Priority: P2)

**Goal**: A generated Russian sample catalog in the documented format.

**Independent Test**: Validate `data/sample-catalog.json` with the loader's validator and see it load.

- [X] T023 [P] [US3] Generator `tools/generate-sample/generate.cs` (a .NET file-based program that does not run in the stack) that writes `data/sample-catalog.json` with `generated: true` and a `source` note, at least 3 categories, at least 3 brands and at least 20 products with Russian names, brands, descriptions, attributes, prices in rubles, quantities and an `image` for every product
- [X] T024 [P] [US3] Generate placeholder photos into `data/images/` locally (no third-party photos) and document their origin in `data/README.md`
- [X] T025 [US3] Unit test in `tests/ShopLoader.UnitTests/SampleCatalogTests.cs` that loads `data/sample-catalog.json` through the validator and asserts: at least 3 categories, 3 brands and 20 products, every product has a photo that exists and a price, all in Russian
- [X] T026 [US3] Run the generator, commit the generated files, and run the loader against a running shop to confirm the products display with photos

**Checkpoint**: SC-003 holds and the sample file validates.

---

## Phase 5: User Story 1 - Start a seeded demo shop with one command (Priority: P1)

**Goal**: `docker-compose up -d` gives a seeded Russian-language shop with no manual steps.

**Independent Test**: On a clean machine with Docker, run `docker-compose up -d`, wait for the loader to exit with code 0, open the local address.

- [X] T027 [US1] Add the `loader` service to `docker-compose.yml`: built from `src/ShopLoader/Dockerfile`, mounts `data/`, depends on `shop` being healthy, runs the loader on `data/sample-catalog.json`, exits with code 0 on success and is safe to re-run
- [X] T028 [US1] Integration test in `tests/ShopLoader.IntegrationTests/SampleDemoTests.cs` that runs the loader image (the Native AOT binary) as a container against the test shop and checks through the extension's list endpoint that the sample catalog is present, then runs it again and checks nothing is duplicated; the real `docker compose up -d` path (including a second `up -d`) is checked by `tools/smoke-test.sh`, which CI runs (covers acceptance scenarios 1-3)
- [X] T029 [US1] Integration test in `tests/ShopLoader.IntegrationTests/SampleDemoTests.cs` that fetches the storefront and a category page and asserts Russian text, a price in rubles and a product photo (covers SC-002 and acceptance scenario 2)
- [X] T030 [US1] Handle a port already in use: document the failure and the `SHOP_PORT` setting in the README, and make `entrypoint.sh` and the compose healthchecks fail with a clear message when the shop cannot start
- [X] T031 [US1] Time a clean first start and record it in the PR description to check SC-005 (under 10 minutes)

**Checkpoint**: Quickstart scenario 1 passes.

---

## Phase 6: User Story 4 - Develop against the stack from outside the containers (Priority: P2)

**Goal**: A development override exposes the shop and database ports for host development.

**Independent Test**: Start with the override, connect from the host, run the loader from the host.

- [X] T032 [US4] Create `docker-compose.dev.yml` exposing the shop on `${SHOP_PORT}` and the database on `${SHOP_DB_PORT}` on the host
- [X] T033 [US4] Make sure the default `docker-compose.yml` never exposes the database port and does not depend on the override; add a test in `tests/ShopLoader.IntegrationTests/ComposeConfigTests.cs` that parses `docker compose config --format json` output for both files and asserts this
- [X] T034 [US4] Verify quickstart scenario 3: run the loader from the host with `dotnet run --project src/ShopLoader` against the dev stack, and record the result

---

## Phase 7: User Story 5 - Integration tests with real containers (Priority: P2)

**Goal**: The test suite runs against real containers locally and in CI. Most tests were written in earlier phases; this phase completes and wires them.

- [X] T035 [P] [US5] Add a smoke integration test in `tests/ShopLoader.IntegrationTests/SampleDemoTests.cs` that runs the Native AOT loader binary from the loader image (not `dotnet run`) against the test shop
- [X] T036 [US5] Check every acceptance scenario of User Story 2 has a matching integration test (SC-007), and that tests are independent of order and leave no containers behind
- [X] T037 [US5] Update `.github/workflows/tests.yml` (job stays named `tests`): set up .NET 10, run `dotnet build`, the unit tests, the integration tests, and `tools/smoke-test.sh` (the Native AOT loader is built inside the loader image, so no clang is needed on the runner); the "no solution yet" fallback is removed now that a solution exists
- [X] T038 [US5] Open a PR from a test branch to confirm the `tests` check passes in GitHub Actions with Docker and fix any issues

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T039 [P] Write the English `README` section for this feature: prerequisites, `docker-compose up -d`, the local address, credentials defaults, the JSON format (link to `contracts/product-file.schema.json`), running the loader on the host, the development override and ports, and changing the port
- [X] T040 [P] Update the roadmap row for 001 in `README.md` to the final status
- [X] T041 Run `specs/001-test-shop/quickstart.md` scenarios 1-4 on a clean machine and fix any gaps
- [X] T042 Re-check the Constitution Check in `specs/001-test-shop/plan.md`; confirm the connector deviation is still isolated in `OpenCartCatalogClient` and note it in the next feature's spec

---

## Dependencies & Execution Order

- **Setup (Phase 1)** has no dependencies.
- **Foundational (Phase 2)** depends on Setup and blocks everything else. T004 (resolving the "Verify" items) comes first because T005, T006 and T008 depend on its answers.
- **US2 (Phase 3)** depends on Foundational. Within it: tests T010-T013 before implementation; T014/T015 then T016, T019, T020, T021, T022; T017/T018 (PHP) can run in parallel with the .NET tasks.
- **US3 (Phase 4)** depends only on the product file format (T014, T016), so it can start in parallel with US2 implementation.
- **US1 (Phase 5)** depends on US2 and US3.
- **US4 (Phase 6)** depends on Foundational and US2.
- **US5 (Phase 7)** depends on all earlier test tasks.
- **Polish (Phase 8)** depends on everything else.

### Parallel Opportunities

- T002, T003 in Setup.
- T010, T011, T012 (tests), and T014, T015 (models and JSON context) in US2.
- T017/T018 (PHP extension) alongside T014-T021 (.NET loader) once the endpoint contract is final.
- T023, T024 in US3.
- T039, T040 in Polish.

---

## Implementation Strategy

- **MVP**: Phases 1-5, which together deliver User Story 1 (the one-command seeded demo) because it needs the extension, loader and sample data.
- **Then**: US4 (dev override) and US5 (CI wiring), then polish.
- Open the PR for implementation once Phase 5 works, and keep later phases as follow-up commits or PRs as needed. The `tests` check must pass before merge.
