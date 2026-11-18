# Feature Specification: Shared Shop Client Library

**Feature Branch**: `002-shared-shop-client`

**Created**: 2026-11-18

**Status**: Draft

**Input**: User description: "Extract the shop API client (currently used by the OpenCart product loader in the test shop feature) into its own shared library, designed so it can support OpenCart and other shops behind a common abstraction. The existing loader must be switched to use this shared library with no change in behavior, and the library must be reusable by the future shop connector project used by the AI assistant."

## Clarifications

### Session 2026-11-18

- Q: How should the loader choose which shop's client to use, given it must contain no shop-specific code? → A: The library provides a factory that creates a client from a shop type; the loader gets an optional command-line flag for the shop type, defaulting to OpenCart when absent.
- Q: What does image upload take: a file path or the image content? → A: The file path, as today. The connector will not create products, so it will not upload images; only the loader does.
- Q: One contract, or split into a general shop part and a catalog-loading part? → A: Split. A general shop contract (readiness) and a separate catalog-loading contract (category and product upsert, image upload); the OpenCart client implements both and the factory can create either.

### Session 2026-11-19

- Q: Should the library retry failed requests, and is that safe given that products must not be duplicated? → A: Yes. Every write is already an upsert by the item's stable key (SKU, slug), so repeating it is safe, with one gap: two overlapping writes for the same new key could both create it (for example a retry sent while the shop is still finishing the timed-out first request). The shop's catalog API therefore serializes writes per key, which makes retries safe.
- Q: Which failures are retried? → A: Lost connections, per-attempt timeouts, and HTTP 408, 429, 500, 502, 503, 504. Never a rejection of the record (400, 404, 409, 415), a rejected API key (401) or a cancellation by the caller. A 500 is retried even though it may be permanent, because it is also how a transient shop error looks.
- Q: Abstract base class with HTTP methods, or a helper? → A: A helper in the library that HTTP-based clients use; the contracts stay interfaces so a shop that does not use HTTP can still implement them.
- Q: Add Polly? → A: No. One policy (status list, exponential backoff with jitter, `Retry-After`) is a few dozen lines; a dependency is not justified yet (Principle VII).
- Q: Does the library validate its inputs and give consumers one exception family? → A: Yes. A common base for the library's shop failures carrying the HTTP status and the underlying error, argument checks before any request, and a connection that validates itself when built.

## Scope

This feature is a refactoring. It moves the code that talks to a shop out of the loader into a library of its own, so that the loader and the future shop connector share one implementation instead of two.

**In scope**: a shared library holding the shop-neutral contracts (general and catalog-loading), a factory, and the OpenCart implementation; switching the loader to use that library; moving the tests that belong to the client with it; documenting how to add a client for another shop.

**Also in scope** (added 2026-11-19): resilience of the client (retries, input validation, a common failure type, bounded waiting) and the one change to the OpenCart extension it needs, serializing writes per key.

**Out of scope** (do not build now): the shop connector itself, its contract for the assistant, any operation the loader does not use today (product search, order creation, order status), a client for any shop other than OpenCart, and any change to the loader's visible behavior (apart from retries), the product file format, or the OpenCart extension (apart from FR-018).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Loader works exactly as before on the shared client (Priority: P1)

A developer runs the loader, locally or as part of `docker-compose up -d`, with the same product file and the same options as before this feature. The loader does the same work, prints the same report and exits with the same codes, but all communication with the shop now goes through the shared library instead of code inside the loader.

**Why this priority**: The refactoring is only worth doing if nothing breaks. Every other story builds on the loader and the library being a clean split.

**Independent Test**: Run the existing loader test suite and the one-command demo unchanged against the refactored code; all pass without edits to their expectations.

**Acceptance Scenarios**:

1. **Given** an empty shop and the sample catalog, **When** the loader runs, **Then** the shop contains the same categories, products, images, prices and stock as before the refactoring.
2. **Given** an already seeded shop, **When** the loader runs again, **Then** nothing is duplicated and only changed items are updated, as before.
3. **Given** a record the shop rejects, **When** the loader runs, **Then** the failing record is reported and the rest are still loaded; **Given** an unreachable shop or wrong credentials, **Then** the loader stops with the same message and exit code as before.
4. **Given** the refactored repository, **When** `docker-compose up -d` is run on a clean machine, **Then** the shop comes up seeded, as before.
5. **Given** the loader's source, **When** it is inspected, **Then** it contains no shop-specific code (no OpenCart addresses, headers or response handling).
6. **Given** the loader is run without the shop type flag, **Then** it uses OpenCart; **Given** it is run with the flag set to OpenCart, **Then** the result is identical; **Given** an unsupported value, **Then** it exits with a clear message listing the supported types.

---

### User Story 2 - Shop-neutral client contract with OpenCart as the first implementation (Priority: P1)

A developer building any tool that works with a shop depends on the shared library and uses shop-neutral contracts: a general shop contract (wait for the shop to be ready) and a separate catalog-loading contract (create or update a category, create or update a product, upload a product image). The OpenCart client is one implementation of both. Nothing in the contract mentions OpenCart, so another shop can be supported by adding a new implementation, and a consumer that only reads from a shop does not depend on the loading operations.

**Why this priority**: Equal to Story 1: it is the reason for the library. Constitution Principle II requires shop-specific code to live behind shop-neutral contracts.

**Independent Test**: Read the contracts and confirm they name no OpenCart concept (apart from the shop type value); run the library's own tests against a real OpenCart shop and confirm the OpenCart client satisfies the contract.

**Acceptance Scenarios**:

1. **Given** the library, **When** its public contracts and types are reviewed, **Then** they contain no OpenCart-specific names, addresses, headers or identifiers, except the shop type value that names OpenCart.
2. **Given** a running OpenCart shop, **When** the OpenCart client is used through the contracts, **Then** it creates and updates categories and products, uploads images and reports created versus updated exactly as the loader relied on before.
3. **Given** the shop rejects one record, **When** the client is used, **Then** it signals a per-record failure that a caller can report and continue from; **Given** the shop cannot be used at all, **Then** it signals a separate failure that tells the caller to stop.
4. **Given** a caller that only knows the contracts, **When** it is given the OpenCart client, **Then** it needs no OpenCart-specific code.
5. **Given** a shop type, **When** the factory is asked for a client, **Then** it returns the matching client; **Given** an unknown shop type, **Then** it fails with a message listing the supported types.

---

### User Story 3 - A second consumer reuses the library without the loader (Priority: P2)

A developer starting the shop connector project references the shared library and uses the general shop contract and the OpenCart client, with no dependency on the loader and without copying code. A developer who wants to support another shop is able to do it by adding one new implementation of the contract, with no change to the existing consumers.

**Why this priority**: This is the payoff of the refactoring, but it can only be proven fully once the connector exists; until then it is checked by a minimal consumer in the tests and by documentation.

**Independent Test**: Build a throwaway consumer that references only the library and performs an upsert against the test shop; confirm it builds and works without referencing the loader.

**Acceptance Scenarios**:

1. **Given** the library, **When** another project references it, **Then** it builds and runs without referencing the loader.
2. **Given** the library documentation, **When** a developer follows it, **Then** they can tell where a new shop's client goes, which contracts it must satisfy and how it is tested, without reading the OpenCart implementation.
3. **Given** a new shop implementation added to the library, **When** the loader and the tests are built, **Then** they need no code change to keep working with OpenCart.

---

### Edge Cases

- A caller wants shop data types (category, product) that today live in the loader's product file model; the library must have its own shop-neutral types so it does not depend on the loader's file format.
- The loader is published as a small, self-contained, fast-starting executable; moving code to a library must not break its packaging or require extra runtime files.
- The tests that cover the client today sit in the loader's test projects; they must neither be lost nor duplicated across the loader and the library (constitution Principle V: a scenario is covered once).
- Two consumers may use different shops at the same time; the library must not hold global state tied to one shop.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shop client code MUST live in its own shared library, separate from the loader and from any consumer.
- **FR-002**: The library MUST expose two shop-neutral contracts covering exactly the operations the loader uses today. The general shop contract has: wait until the shop is ready. The catalog-loading contract has: create or update a category, create or update a product (reporting whether it was created or updated and the shop's identifier), and upload a product image (given as a path to a local file, as today). A consumer MUST be able to use the general contract without the catalog-loading one, and a shop's client MAY implement the general contract alone.
- **FR-003**: The library's public contracts and types MUST NOT contain any OpenCart-specific concept, other than OpenCart being one named shop type; all other OpenCart specifics MUST be confined to the OpenCart implementation.
- **FR-004**: The library MUST contain an OpenCart implementation of both contracts that behaves identically to the client currently inside the loader (same requests, same authentication, same handling of per-record and whole-shop failures), apart from the resilience added by FR-015 to FR-019.
- **FR-005**: The library MUST distinguish a failure of one record (caller may continue) from the shop being unusable (caller must stop), through failure types defined in the library.
- **FR-006**: The library MUST define its own shop-neutral category and product types, independent of the loader's product file format; the loader MUST convert its file records to them.
- **FR-007**: The loader MUST use the library for all communication with the shop and MUST contain no shop-specific code, including the choice of client.
- **FR-007a**: The library MUST provide a factory that creates a client for either contract from a shop type; adding a shop MUST NOT require changing the factory's callers. An unknown shop type MUST be rejected with a clear message listing the supported types.
- **FR-007b**: The loader MUST accept an optional command-line flag naming the shop type; when it is absent the shop type MUST be OpenCart, so existing invocations behave exactly as before.
- **FR-008**: The loader's observable behavior MUST NOT change, apart from the optional flag of FR-007b and the retries of FR-015: arguments, environment settings, report format, messages, exit codes and the resulting shop content stay the same.
- **FR-009**: The loader MUST keep its current packaging (single self-contained executable, same container image and one-command start) after the change.
- **FR-010**: The library MUST NOT depend on the loader, and MUST be referenceable by another project without bringing the loader along.
- **FR-011**: The library MUST NOT access the shop's database; it uses only the shop's API (Principle II).
- **FR-012**: Tests that cover client behavior MUST move out of the loader's tests and follow the constitution's testing rules (unit tests for logic that needs no shop, in the library's own unit test project; integration tests against the real shop, in the repository's one real-shop integration project, which owns the container setup; no stand-ins for the shop; shared helpers in the base classes). The loader's tests MUST cover only the loader's own behavior.
- **FR-013**: The repository MUST document the library: its purpose, the contracts, who uses it, and how to add a client for another shop.
- **FR-014**: The `tests` workflow MUST keep passing with the new projects included, and the README and agent context MUST reflect the new structure.
- **FR-015**: Requests that fail in a way that may be temporary MUST be retried by the library: a lost connection, a request that exceeds its time limit, and HTTP 408, 429, 500, 502, 503 and 504. A rejection of the record (400, 404, 409, 415), a rejected API key (401), an unknown host or a TLS failure (which do not fix themselves) and a cancellation by the caller MUST NOT be retried. The number of retries MUST be configurable on the connection (default 3; 0 turns retries off) as MUST the wait before the first retry (default 0.5 s), waits between attempts MUST grow exponentially with jitter up to a cap of 10 s, and a `Retry-After` answer MUST be honored within that cap. Only requests that are safe to repeat (every request the library makes today) may be retried.
- **FR-016**: When retries are exhausted the library MUST raise the same failure type as without retries, naming the number of attempts (when more than one) and keeping the last underlying error, so consumers need no new handling.
- **FR-017**: Retry logic MUST live in a shared helper inside the library that any HTTP-based client can use; the contracts MUST NOT depend on HTTP, and no third-party resilience package is added.
- **FR-018**: The OpenCart catalog API MUST serialize writes to the same item key (SKU, slug), so that two overlapping writes for one new key produce exactly one item. Writes to different keys MUST NOT wait for each other.
- **FR-019**: The library MUST check its inputs before any request (null or blank keys and required fields, negative price or stock, a connection whose address is not an absolute http or https address, has a blank API key or a non-positive timeout) and raise the standard argument exceptions; every failure about a shop MUST derive from one library base failure type carrying the HTTP status (when there was an answer) and the underlying error; waiting for the shop to become ready MUST NOT exceed its overall timeout because of one slow attempt; an unreadable image file and an answer that is not valid JSON MUST be per-record failures; HTTP 408, 429, 502, 503 and 504 that persist after retries MUST be shop-unavailable failures.

### Key Entities

- **Shop type**: The name of a supported shop (initially only OpenCart) from which the factory creates the matching client.
- **Shop contract**: The general shop-neutral operations on a shop (readiness).
- **Catalog-loading contract**: The shop-neutral operations for filling a shop's catalog (category upsert, product upsert, image upload); used by the loader only.
- **OpenCart client**: The implementation of the contract for OpenCart, using the shop's catalog API extension.
- **Catalog category and catalog product**: Shop-neutral descriptions of a category and a product (identifying code, names, price, stock, category links, attributes) used by the contract, independent of any file format.
- **Upsert result**: The shop's identifier for an item and whether it was created or updated.
- **Per-record failure / shop-unavailable failure**: The two kinds of failure a consumer must be able to tell apart.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The complete existing automated test suite passes after the refactoring with no change to the expected results of the loader and demo scenarios.
- **SC-002**: On a clean machine, `docker-compose up -d` produces the same seeded shop as before, with the same number of categories, products and images, in the same time budget (under 10 minutes).
- **SC-003**: The loader contains 0 references to OpenCart-specific names, addresses or response formats; all of them are in the library's OpenCart implementation.
- **SC-004**: A new project can use the client by adding one reference to the library, with 0 references to the loader and 0 copied files.
- **SC-005**: A developer new to the repository can find where to add a client for another shop, and what it must implement, from the documentation alone in under 10 minutes.
- **SC-006**: No client scenario is tested in two places: each behavior of the client is covered once, at the level that proves more.
- **SC-007**: A shop that fails the first two attempts of a request and then answers causes no failed record and no duplicate; two simultaneous writes of the same new product produce exactly one product.
- **SC-008**: A shop that never answers makes a request fail after (retries + 1) × the request time limit plus the waits, and never longer.

## Assumptions

- This feature does not add operations; reads (search, product lookup) and orders are added to the general shop contract by the connector feature when needed (Principle VII). The connector is not expected to create products, so it will not use the catalog-loading contract.
- The contract's exact shape may be adjusted by the connector feature; this feature fixes only that it is shop-neutral and that the loader's current operations are in the two contracts.
- The OpenCart catalog API extension and the product file format are unchanged; the product file stays a loader concern.
- Existing shop and loader behavior, including the AOT-compatible, source-generated serialization already in use, is preserved; the library is built to the same constraints so the loader can still be published as it is today.
- The library is internal to this repository and is consumed as a project reference; publishing it as a package is out of scope.
- The work follows the usual flow: spec, plan, tasks, implementation, then a pull request with a short description and a passing `tests` check.
