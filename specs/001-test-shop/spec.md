# Feature Specification: Test Electronics Shop (Local Demo)

**Feature Branch**: `001-test-shop`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Smallest useful slice of the test shop: run an OpenCart shop locally with `docker-compose up -d`, load products into it from a JSON file with a loader script, and include a small generated sample catalog so a working Russian-language demo shop comes up with a single command. Everything else (public address, connector, order API, scraped catalog) comes in later features."

## Scope

This feature is deliberately small. It proves that a seeded demo shop can be started with one command.

**In scope**: OpenCart running in containers, a loader that fills the shop from a JSON file through the shop's API, a small generated sample catalog in Russian, a development configuration that exposes ports.

**Deferred to later features** (do not build now): public address for the shop, the shop connector and its shop-neutral contract, order creation and order status via an external service, administrator status flows, stub payments, reset command, a large catalog snapshot from a real retailer.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start a seeded demo shop with one command (Priority: P1)

A developer or blog reader clones the repository and runs `docker-compose up -d`. After the services finish starting, the shop is available in a browser on the local machine and already contains the sample products, with no further manual steps.

**Why this priority**: This is the whole point of the slice: an easy, reproducible demo shop for the blog series and for development.

**Independent Test**: On a clean machine with Docker, run `docker-compose up -d`, wait until it finishes, open the local shop address and browse the sample catalog.

**Acceptance Scenarios**:

1. **Given** a clean machine with Docker installed, **When** the user runs `docker-compose up -d`, **Then** OpenCart is installed and configured automatically with no manual installer steps.
2. **Given** the services have started, **When** the user opens the documented local address, **Then** a Russian-language storefront with the sample categories and products is shown.
3. **Given** the stack was already started and seeded, **When** `docker-compose up -d` is run again, **Then** nothing is duplicated and the shop keeps working.
4. **Given** a required local port is already in use, **When** the stack starts, **Then** the failure is clear and documented, with a documented way to change the port.

---

### User Story 2 - Load products from a JSON file through the shop API (Priority: P1)

A developer runs the loader, which reads a JSON file of categories and products and creates them in the shop using only the shop's official programmatic interface. Running it again does not create duplicates: existing items are updated and new ones are added. The loader also runs automatically as part of `docker-compose up -d`.

**Why this priority**: Makes the catalog reproducible and swappable (the sample data now, a real snapshot later) and proves that the shop can be filled without touching its database.

**Independent Test**: Start an empty shop, run the loader, count items; run it again and verify the counts are unchanged.

**Acceptance Scenarios**:

1. **Given** an empty shop and a valid JSON file, **When** the loader runs, **Then** all categories, products, images, prices and stock levels from the file appear in the shop.
2. **Given** an already seeded shop, **When** the loader runs again with the same file, **Then** no duplicate categories or products are created.
3. **Given** a file where one product's price changed, **When** the loader runs, **Then** that product's price is updated and no other product changes.
4. **Given** a malformed record in the file, **When** the loader runs, **Then** it reports which record failed and continues with the rest.
5. **Given** the loader, **When** it runs, **Then** it never reads or writes the shop's database directly.

---

### User Story 3 - Generated sample catalog (Priority: P2)

The repository includes a small generated sample catalog in the JSON format: a few electronics categories and a few dozen products with Russian names, brands, attributes, prices in rubles, stock and placeholder photos. It is clearly marked as generated, not copied from a real retailer.

**Why this priority**: Gives User Stories 1 and 2 realistic data now, without the effort and legal questions of a real snapshot.

**Independent Test**: Open the JSON file, validate it against the documented format, and see the products in the running shop.

**Acceptance Scenarios**:

1. **Given** the sample file, **When** it is inspected, **Then** it has at least 3 categories, at least 3 brands and at least 20 products, all in Russian with prices in rubles.
2. **Given** the sample file, **When** it is validated, **Then** every product has name, category, brand, attributes, price, availability and a photo.
3. **Given** the sample file, **When** the loader loads it, **Then** the products display in the shop with their photos.

---

### User Story 4 - Develop against the stack from outside the containers (Priority: P2)

A developer starts the stack with a development override that exposes the ports of the shop and its database on the local machine, so the loader and later services can run and be debugged on the host.

**Why this priority**: Required by the constitution and by everyday development, but the demo works without it.

**Independent Test**: Start the stack with the development override, connect to the shop and the database from the host, and run the loader from the host against the shop.

**Acceptance Scenarios**:

1. **Given** the development override, **When** the stack starts, **Then** the shop and its database are reachable on documented local ports.
2. **Given** the default configuration without the override, **When** the stack starts, **Then** the database port is not exposed to the host.
3. **Given** the development override, **When** the loader is run from the host, **Then** it loads the catalog into the running shop.

---

### User Story 5 - Integration tests with real containers (Priority: P2)

A developer runs the automated test suite and it starts real OpenCart and database containers with Testcontainers, runs the loader against them and checks the result through the shop's API. No mocks stand in for the shop. The same tests run in the GitHub Actions check.

**Why this priority**: The loader's correctness depends on how the real shop behaves, so mocks would hide the failures that matter. It also keeps the tests an honest part of the blog series.

**Independent Test**: Run the test suite on a machine with Docker and see the tests start containers, load the sample catalog and pass.

**Acceptance Scenarios**:

1. **Given** Docker is available, **When** the integration tests run, **Then** they start a fresh OpenCart and database from the same images and configuration as the demo, and remove them afterwards.
2. **Given** a fresh shop container, **When** the tests run the loader with the sample catalog, **Then** they verify through the shop's API that the categories and products exist with the right prices and stock.
3. **Given** a seeded test shop, **When** the tests run the loader a second time, **Then** they verify that no duplicates appear, and that a changed price is applied.
4. **Given** a file with a malformed record, **When** the tests run the loader, **Then** they verify that the record is reported and the others are loaded.
5. **Given** the test suite, **When** it runs, **Then** each test starts from a known state and does not depend on the order of other tests.

---

### Edge Cases

- A product has no photo or no price; the loader skips and reports it.
- The file contains the same product twice; only one product is created.
- Names or attributes contain Cyrillic text, special characters or very long values; they are stored and displayed correctly.
- The shop's programmatic interface is not ready yet when the loader starts; the loader waits and retries for a bounded time, then fails clearly, and can be re-run safely.
- The JSON file is missing, not valid JSON, or in the wrong format; the loader fails with a clear message before changing anything.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shop MUST be OpenCart, with no custom shop software, and MUST present a Russian-language storefront with prices in rubles.
- **FR-002**: The whole environment (shop, database and loader) MUST run in containers and start with `docker-compose up -d` as the single documented command.
- **FR-003**: The setup MUST automate installing and configuring OpenCart, enabling its programmatic interface and seeding the catalog, with no manual steps beyond installing Docker.
- **FR-004**: The setup MUST be safe to re-run without duplicating data or breaking an existing installation.
- **FR-005**: The product file MUST be in a documented JSON format with categories and, for each product, name, category, brand, attributes, price, photo and availability.
- **FR-006**: The loader MUST create and update categories, products, images, prices and stock in the shop from the JSON file.
- **FR-007**: The loader MUST use only the shop's official programmatic interface and MUST NOT access the shop's database directly.
- **FR-008**: The loader MUST be idempotent: re-running it MUST NOT create duplicate categories or products and MUST apply changed values.
- **FR-009**: The loader MUST report records it could not load and continue with the remaining records, and MUST validate the file before changing anything.
- **FR-010**: The repository MUST include a generated sample catalog in Russian that follows the documented format and is clearly marked as generated.
- **FR-011**: A development override configuration MUST expose the shop and its database on local ports; the default configuration MUST NOT depend on it and MUST NOT expose the database.
- **FR-012**: Credentials MUST come from configuration with documented non-secret defaults for local use, and no real secrets MUST be committed.
- **FR-013**: Setup and operation (starting, loading data, the JSON format, the development override, ports) MUST be documented in English.
- **FR-014**: The loader MUST have automated tests, and a GitHub Actions check MUST verify that the stack starts and the sample catalog loads.
- **FR-015**: Integration tests MUST use Testcontainers to start real OpenCart and database containers, with no mocked shop, and MUST cover loading, idempotency, price updates and malformed records. They MUST clean up their containers and run both locally and in the GitHub Actions check.

### Key Entities

- **Product File**: A JSON file with categories and products in the documented format.
- **Category**: A grouping of products in the shop's catalog.
- **Product**: A catalog item with brand, attributes, photo, price in rubles and availability.
- **Loader**: The tool that reads a product file and fills the shop through its programmatic interface.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a clean machine with Docker, `docker-compose up -d` results in a shop with the sample catalog visible in the browser, with no other manual steps.
- **SC-002**: The storefront shows a Russian-language product listing within 5 seconds of opening the local address once the stack is up.
- **SC-003**: The sample catalog has at least 20 products in at least 3 categories and 3 brands, and 100% of products have a photo and a price.
- **SC-004**: A second loader run on the seeded shop creates 0 duplicates, and a changed price is applied.
- **SC-005**: The full first start, including seeding, completes in under 10 minutes on a typical developer machine.
- **SC-007**: The integration tests cover every acceptance scenario of User Story 2 against real containers, pass on a clean run in the GitHub Actions check, and leave no containers behind.
- **SC-006**: With the development override, a developer can run the loader from the host against the running shop following the documentation.

## Assumptions

- The shop's official programmatic interface is enough to create categories, products, images, prices and stock. If it cannot, how to close the gap without direct database access is a planning decision.
- Docker with the Compose plugin is the only prerequisite. `docker-compose up -d` is the documented command, and `docker compose up -d` is equivalent.
- The loader runs as a container in the default configuration and can also run on the host in the development override. Its implementation language follows the constitution.
- The constitution's shop connector rule (Principle II) is not fully implemented in this slice. The loader's OpenCart-specific code is kept isolated in one place so it can move into the connector in the next feature; the plan records this as a justified, temporary deviation.
- Testcontainers is the library for integration tests, since the backend is .NET and it supports it. The tests reuse the demo's images and configuration so they test what readers actually run.
- Placeholder photos are generated or taken from permissively licensed sources, and their origin is documented.
- Out of scope: everything listed under "Deferred to later features".
