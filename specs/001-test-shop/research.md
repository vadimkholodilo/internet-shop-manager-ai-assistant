# Research: Test Electronics Shop (Local Demo)

Items marked **Resolved** were settled by experiment during implementation (OpenCart 4.1.0.4, PHP 8.2, MariaDB 11.4).

## R1. How to write catalog data through an API

- **Decision**: Write a small OpenCart extension (`catalog_api`) in this repo that exposes an authenticated
  catalog upsert endpoint and calls OpenCart's own admin models (`addProduct`, `editProduct`, category
  equivalents). The loader calls only this endpoint.
- **Rationale**: The searches found no core route for creating products; core `api/` routes cover cart,
  customer and order for storefront use. The extension uses OpenCart's own code, so it respects "no direct
  database access", and it is version-controlled and installed automatically.
- **Confirmed in the 4.1.0.4 source**: the native `catalog/controller/api/` routes cover only affiliate, cart,
  customer, addresses, shipping and payment methods, order and subscription; there is no product or category
  route. The native order API is expected to serve the connector in a later feature.
- **Alternatives considered**: Driving the admin UI with form posts (rejected: fragile, token handling,
  many form fields); A marketplace "Dynamic API" extension exposing models (unverified for
  OpenCart 4, third party); the Webkul REST add-ons (commercial, marketplace-oriented); direct SQL (forbidden by the constitution).
- **Resolved**: The extension is a catalog-side controller (`catalog/controller/catalog_api/api.php`, route
  `catalog_api/api`, exposed as `/catalog-api/...` by an Apache rewrite). It registers the `Opencart\Admin`
  namespace with the autoloader and pre-registers the admin models (`design/seo_url`, `catalog/category`,
  `catalog/product`, `catalog/manufacturer`, `catalog/attribute`, `catalog/attribute_group`) as the loader's
  fallback objects, because admin models call each other through `$this->load->model()` and would otherwise
  resolve same-named catalog models. Authentication is a bearer token compared with `SHOP_API_KEY` (passed
  to PHP with Apache `PassEnv`). Writes run in a database transaction and roll back on any error. The SKU is
  stored in the product's `model` field and the category slug as its SEO keyword.

## R2. Getting an OpenCart image

- **Decision**: Build our own image from a pinned OpenCart 4.1.0.4 release on a `php:*-apache` base.
- **Rationale**: The Bitnami OpenCart image is deprecated, and no maintained official image was found.
  A pinned build keeps the demo reproducible and lets us bake in the extension.
- **Alternatives considered**: `bitnami/opencart` (deprecated); `byjg/opencart` (OpenCart 3.x, no
  releases); a community image (unclear maintenance).
- **Resolved**: OpenCart 4.1.0.4 on `php:8.2-apache` (the pair upstream's own Dockerfile uses). The CLI installer
  `install/cli_install.php install ...` works unattended.

## R3. Unattended installation

- **Decision**: The entrypoint waits for MariaDB, runs OpenCart's CLI installer once (guarded by a marker
  such as an existing config file), then enables the extension and creates the API key from environment
  variables. Re-runs skip finished steps.
- **Rationale**: Gives zero manual installer steps and safe `docker-compose up -d` re-runs.
- **Alternatives considered**: Committing a database dump (a binary snapshot hides state and is
  effectively loading through the database).

## R4. Russian language and rubles

- **Decision**: Install the Russian language pack, set it as the default, and set the default currency to RUB
  during first-run configuration.
- **Resolved**: The installer only ships `en-gb` and `fr-fr`, and no maintained OpenCart 4 Russian pack exists.
  The image copies every `en-gb` language directory to `ru-ru` and overlays Russian strings for the pages a
  shopper sees (`shop/language/ru-translations.php`, applied at build time by `shop/language/apply.php`);
  untranslated keys stay English. Provisioning renames the single installed language to `ru-ru`, enables only
  the RUB currency (no decimals, symbol after the amount), translates stock statuses and the page title, and
  removes OpenCart's English demo catalog once.

## R5. Loader language and shape

- **Decision**: .NET 10 console app using `System.Text.Json` (source-generated context) and `HttpClient`,
  with hand-written validation before any request, published with Native AOT. Idempotency key is the product SKU (or a stable `id` from the file) and category slug.
- **Rationale**: Constitution Principle I; the same code is reused by the connector later.
- **Alternatives considered**: Python script (not justified by Principle I); reflection-based JSON, CLI or
  schema-validation libraries (break or bloat Native AOT, so they are avoided).
- **Resolved**: No dependencies beyond the framework; the build has no trim or AOT warnings
  (`TreatWarningsAsErrors`). The binary is built in the Docker image; linking on a Homebrew .NET host on macOS
  fails with `library 'ssl' not found`, so development there uses `dotnet run`.

## R5a. Native AOT scope

- **Decision**: Use Native AOT for the loader, the only shipped .NET program. Build it in the Dockerfile with
  the .NET 10 SDK image plus the native toolchain (clang, zlib headers), and ship the binary in a
  `runtime-deps` image. Build per architecture natively, so Apple Silicon and x64 both work.
- **Rationale**: Small image, fast start (the loader runs on every `docker-compose up -d`), no runtime
  install.
- **Alternatives considered**: Framework-dependent publish (larger image, slower start); AOT for tests (not
  possible with xUnit and Testcontainers, which use reflection, so tests use the normal runtime).
- **Resolved**: An integration test runs the loader image (the AOT binary) as a container against the test shop.

## R6. Readiness and ordering at start-up

- **Decision**: Compose healthchecks on MariaDB and OpenCart (an HTTP check against the storefront and the
  endpoint). The loader container starts only when OpenCart is healthy, retries with a bounded timeout, and
  exits with code 0 after a successful load.
- **Rationale**: Satisfies the edge case "API not ready yet" and the single-command requirement.

## R7. Integration tests

- **Decision**: Testcontainers for .NET with the Compose module, using the same `docker-compose.yml`
  (without the loader service) so tests exercise what readers run. Wait on the compose healthchecks. Each
  test class starts from a fresh stack or uses unique SKUs so tests do not depend on order.
- **Rationale**: Spec User Story 5; works on `ubuntu-latest`, where Docker is available.
- **Resolved**: Testcontainers' `ComposeBuilder` runs `docker compose` inside a container and cannot build our
  local build contexts, so the tests use Testcontainers' image and container builders with the repository's own
  Dockerfiles (`shop/Dockerfile`, `src/ShopLoader/Dockerfile`) and the same environment as `docker-compose.yml`.
  The real `docker compose up -d` path is covered by `tools/smoke-test.sh`, run in CI. One OpenCart install takes
  about 10 seconds, so one stack is shared per test run and tests isolate themselves with unique SKUs.

## R8. Sample data and photos

- **Decision**: A small generator under `tools/generate-sample/` writes `data/sample-catalog.json` with
  Russian names and plausible prices, plus placeholder images generated locally (no third-party photos).
- **Rationale**: Avoids licensing questions; keeps the file marked as generated.

## R9. Continuous integration

- **Decision**: Move `.github/workflows/tests.yml` to .NET 10 and extend it to build the solution, publish the loader with Native AOT, run unit tests, then run the
  Testcontainers tests. The job stays named `tests` so branch protection keeps working.
