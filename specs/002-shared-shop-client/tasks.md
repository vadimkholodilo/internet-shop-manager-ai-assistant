---

description: "Task list for the Shared Shop Client Library"
---

# Tasks: Shared Shop Client Library

**Input**: Design documents from `/specs/002-shared-shop-client/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/shop-client-api.md, quickstart.md

**Tests**: Included. The constitution (Principle V) makes tests mandatory, and the spec requires moving and adding client tests (FR-012). Follow its rules: no stand-ins for the shop, shared helpers in `UnitTest` and `ShopTest`, each scenario covered once.

**Organization**: Tasks are grouped by user story. This is a refactoring: the existing loader tests are the safety net and MUST keep their expectations unchanged.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)
- Paths are relative to the repository root

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the empty library and its test project and wire them into the solution

- [X] T001 Create `src/ShopClient/ShopClient.csproj`: class library, `RootNamespace` and `AssemblyName` `ShopClient`, `IsAotCompatible`, `EnableTrimAnalyzer`, `EnableAotAnalyzer`, `JsonSerializerIsReflectionEnabledByDefault=false` (same settings as `src/ShopLoader/ShopLoader.csproj`, but no `PublishAot`, `OutputType` or `System.CommandLine`), `InternalsVisibleTo` for `ShopClient.UnitTests` and `ShopLoader.IntegrationTests`; must not reference the loader
- [X] T002 [P] Create `tests/ShopClient.UnitTests/ShopClient.UnitTests.csproj` modelled on `tests/ShopLoader.UnitTests/ShopLoader.UnitTests.csproj` (xunit, `Using Xunit`), with a project reference to `src/ShopClient/ShopClient.csproj` only
- [X] T003 Add `src/ShopClient/ShopClient.csproj` (under `/src/`) and `tests/ShopClient.UnitTests/ShopClient.UnitTests.csproj` (under `/tests/`) to `InternetShop.slnx`
- [X] T004 [P] Add a `dotnet test tests/ShopClient.UnitTests --configuration Release --no-build` step next to the other unit tests in `.github/workflows/pull-request.yml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The library's public surface and the moved OpenCart client, as specified in `contracts/shop-client-api.md` and `data-model.md`

**⚠️ CRITICAL**: No user story can begin until this phase is complete. The loader does not build between T009 and T012; that is expected, finish through US1 before building the whole solution (build `src/ShopClient` alone to check this phase).

- [X] T005 [P] Create `src/ShopClient/CatalogCategory.cs`, `src/ShopClient/CatalogProduct.cs` and `src/ShopClient/UpsertResult.cs` as public records with the fields in `data-model.md` (`CatalogCategory(Slug, Name, Parent?)`, `CatalogProduct(Sku, Name, Category, Brand, Description, Attributes, Price, Quantity)` with price `decimal` in rubles and quantity `int` of "0 or more", `UpsertResult(long Id, bool Created)`)
- [X] T006 [P] Create `src/ShopClient/Exceptions.cs` with public `CatalogException` (one record rejected, caller continues),  `ShopUnavailableException` (shop unusable, caller stops) and `UnsupportedShopException` (unknown shop type or missing capability); messages stay as in the current loader
- [X] T007 [P] Create `src/ShopClient/IShopClient.cs` (`IDisposable`, `WaitUntilReadyAsync(TimeSpan, CancellationToken)`) and `src/ShopClient/ICatalogLoadingClient.cs` (extends `IShopClient`; `UpsertCategoryAsync`, `UpsertProductAsync`, `UploadImageAsync(string sku, string imagePath, ...)` with the image as a local file path), with XML doc comments that mention no OpenCart concept; every async method takes `CancellationToken cancellationToken = default` so callers need not pass `CancellationToken.None`
- [X] T008 [P] Create `src/ShopClient/ShopType.cs` (`OpenCart` value, case-insensitive `TryParse`, `SupportedNames`; hand-written name table, no `Enum.Parse`) and `src/ShopClient/ShopConnection.cs` (`Url`, `ApiKey`, `RequestTimeout` as a non-null `TimeSpan` init property defaulting to 60 seconds on the connection, so clients need no fallback)
- [X] T009 Move the OpenCart client with `git mv`: `src/ShopLoader/OpenCart/OpenCartCatalogClient.cs` to `src/ShopClient/OpenCart/OpenCartClient.cs` (internal class `OpenCartClient : ICatalogLoadingClient`, takes `ShopConnection`) and `src/ShopLoader/Json/LoaderJsonContext.cs` to `src/ShopClient/OpenCart/OpenCartJsonContext.cs` (internal, snake_case naming kept); keep the optional `CancellationToken cancellationToken = default` parameter on its public methods; switch it to `CatalogCategory`/`CatalogProduct`/`UpsertResult` and the library's exceptions; behavior, requests, messages and `401` handling identical (depends on T005–T008)
- [X] T010 Create `src/ShopClient/ShopClientFactory.cs` with `CreateShopClient` and `CreateCatalogLoadingClient` taking `ShopType` and `ShopConnection`, returning the OpenCart client, and throwing `UnsupportedShopException` listing `ShopType.SupportedNames` for an unknown type or a client that lacks the requested contract (depends on T009)

**Checkpoint**: `dotnet build src/ShopClient` succeeds and the library has no reference to the loader.

---

## Phase 3: User Story 1 - Loader works exactly as before on the shared client (Priority: P1) 🎯 MVP

**Goal**: The loader communicates with the shop only through the library, with unchanged behavior, plus the optional `--shop` flag.

**Independent Test**: The existing loader unit and integration tests and `tools/smoke-test.sh` pass with no change to their expectations.

### Tests for User Story 1

- [X] T011 [P] [US1] In `tests/ShopLoader.UnitTests/CliTests.cs` (and the command-line option helper in `tests/ShopLoader.UnitTests/Support/UnitTest.cs` if it builds option strings) add cases: `--shop opencart` is accepted, and `--shop nope` exits with 2 and an error that lists the supported types; no new test for "default is OpenCart" (the existing tests without the flag cover it)

### Implementation for User Story 1

- [X] T012 [US1] In `src/ShopLoader/ShopLoader.csproj` add a project reference to `../ShopClient/ShopClient.csproj` and keep `InternalsVisibleTo` for `ShopLoader.UnitTests` (the unit tests use `Cli` and the validator); remove the `ShopLoader.IntegrationTests` entry only if the build shows it is unused; delete `src/ShopLoader/ICatalogClient.cs` (its types now live in the library)
- [X] T013 [US1] In `src/ShopLoader/Loader.cs` take `ICatalogLoadingClient` and map `CategoryRecord`/`ProductRecord` to `CatalogCategory`/`CatalogProduct` before each upsert; catch the library's `CatalogException`; keep all output text unchanged
- [X] T014 [US1] In `src/ShopLoader/Cli.cs` add the optional `--shop` option (default `opencart`, parsed with `ShopType.TryParse`, an unsupported value is a usage error with exit code 2 listing `ShopType.SupportedNames`), create the client with `ShopClientFactory.CreateCatalogLoadingClient(...)`, and remove the `ShopLoader.OpenCart` using; the loader then contains no OpenCart name
- [X] T015 [US1] Update `src/ShopLoader/Dockerfile` to `COPY src/ShopClient/ src/ShopClient/` before publishing (build context stays the repository root)
- [X] T016 [US1] Move `tests/ShopLoader.UnitTests/OpenCartCatalogClientTests.cs` to `tests/ShopClient.UnitTests/OpenCartClientTests.cs` with `git mv` (use the renamed client, `CatalogCategory` and the library's exceptions; keep all its scenarios unchanged) so the loader's unit tests compile; depends on T009
- [X] T017 [US1] Run `dotnet build`, `dotnet test tests/ShopLoader.UnitTests` and `dotnet test tests/ShopLoader.IntegrationTests`, then `tools/smoke-test.sh`; fix any behavior difference in the code, never in the expectations of existing tests

**Checkpoint**: The loader runs on the library and the demo is unchanged. This is the MVP.

---

## Phase 4: User Story 2 - Shop-neutral contract with OpenCart as the first implementation (Priority: P1)

**Goal**: The library's contract is demonstrably shop-neutral, the OpenCart client satisfies it against a real shop, and the client's tests live with the library.

**Independent Test**: `dotnet test tests/ShopClient.UnitTests` and `dotnet test tests/ShopLoader.IntegrationTests --filter ShopClientTests` pass; the public contract has no OpenCart names.

### Tests for User Story 2

- [X] T018 [US2] Review the moved `tests/ShopClient.UnitTests/OpenCartClientTests.cs` (moved in T016): it keeps its real-socket scenarios and uses no stand-in for the shop
- [X] T019 [P] [US2] Create `tests/ShopClient.UnitTests/ShopClientFactoryTests.cs`: each supported `ShopType` yields a client for both contracts; an unsupported type (`default(ShopType)`, which has no name) fails with a message that lists `ShopType.SupportedNames`; and `ShopType.TryParse` accepts `opencart` in any case and rejects other text (one test class for both, no repeats of other tests)
- [X] T020 [US2] Create `tests/ShopLoader.IntegrationTests/ShopClientTests.cs` deriving from `ShopTest`: using only `ShopClientFactory` and the contracts against the real shop, upsert a category and a product (assert `Created` true then false on repeat, and the shop shows the update, verified through `Api` in `Support/CatalogApi.cs`), upload an image, and a rejected record raises `CatalogException` (the wrong-API-key case is already covered by `LoaderTests.WrongApiKeyIsRejectedWithAClearMessage`, do not repeat it); add any connection helper to `Support/ShopTest.cs`, no URL paths or response fields in the test class

### Implementation for User Story 2

- [X] T021 [US2] Verify the contract is shop-neutral: confirm the public types in `src/ShopClient/*.cs` (outside `OpenCart/`) contain no `OpenCart`, `catalog-api` or `Authorization` text (`grep -rniE "opencart|catalog-api" src/ShopClient --include=*.cs` matches only `ShopType.cs`'s `OpenCart` value, `ShopClientFactory.cs` and the `OpenCart/` folder), and that `grep -rniE "opencart|catalog-api" src/ShopLoader --include=*.cs` prints nothing; fix any leak in code or doc comments
- [X] T022 [US2] Run `dotnet test tests/ShopClient.UnitTests` and `dotnet test tests/ShopLoader.IntegrationTests --filter ShopClientTests`; ensure no client scenario is duplicated between `ShopClientTests`, `LoaderTests` and `CatalogApiTests` (remove the weaker copy if one is found)

**Checkpoint**: The library is verified on its own and the client tests no longer live in the loader.

---

## Phase 5: User Story 3 - A second consumer reuses the library without the loader (Priority: P2)

**Goal**: Another project can use the library by one reference, and a developer can learn how to add another shop from the docs alone.

**Independent Test**: The library builds without the loader; `ShopClientTests` (T020) acts as the second consumer; following the README "Adding a shop" section is understandable on its own.

- [X] T023 [P] [US3] Write `src/ShopClient/README.md`: purpose, who uses it (the loader now, the connector later), the two contracts and what each is for, the factory and `ShopType`, the failure types and how callers react, and an "Adding a shop" section (new internal folder implementing the contracts, add a `ShopType` value, register it in `ShopClientFactory`, add tests through `ShopClientTests`)
- [X] T024 [US3] Verify independence: `src/ShopClient/ShopClient.csproj` has no reference to the loader (`grep -c ShopLoader src/ShopClient/ShopClient.csproj` prints 0), `dotnet build src/ShopClient` succeeds alone, the library project has no database client package reference (FR-011), and `dotnet publish src/ShopLoader` (Native AOT, as in the Dockerfile) shows no trim or AOT warnings from the library

**Checkpoint**: All three stories are met.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T025 [P] Update `README.md` (project structure and the loader command with the new `--shop` flag, also described in the `--shop` option's help text in `src/ShopLoader/Cli.cs`), `tests/README.md` (new `ShopClient.UnitTests` row and where client tests go) and `CLAUDE.md` (mention `src/ShopClient` and this feature next to `001-test-shop`)
- [X] T026 Run `dotnet format InternetShop.slnx --verify-no-changes` and fix findings; run the whole suite and `tools/smoke-test.sh` once more
- [X] T027 Run the validation scenarios in `specs/002-shared-shop-client/quickstart.md` end to end

---

## Phase 7: Resilience (added 2026-11-19; FR-015 to FR-019, SC-007, SC-008)

**Goal**: Temporary shop errors do not fail records or runs, and retries can never create duplicates.

**Independent Test**: A shop that fails twice and then answers produces no failed record; two simultaneous writes of one new SKU produce one product.

- [X] T028 [P] Extend `ShopConnection` (`src/ShopClient/ShopConnection.cs`) with `MaxRetries` (default 3) and `RetryDelay` (default 0.5 s), both validated not negative; update the library README
- [X] T029 Write the internal retry helper `src/ShopClient/HttpRetry.cs` per research R9 (statuses, backoff with jitter capped at 10 s, `Retry-After` within the cap, a fresh request per attempt, caller cancellation never retried, attempts named in the final failure with the last error inside)
- [X] T030 Use it in `src/ShopClient/OpenCart/OpenCartClient.cs` for the upserts and the image upload (build the request, including content, inside the function passed to the helper); `WaitUntilReadyAsync` keeps its own loop
- [X] T031 [P] Unit tests in `tests/ShopClient.UnitTests` (real sockets, a scripted listener that answers a list of responses in order): 503 twice then success makes no failure; 400/404/409/401 are not retried; exhausted retries throw the original failure type with the attempt count; `MaxRetries = 0` makes one attempt; `Retry-After` is honored; cancellation is not retried
- [X] T032 In `shop/extension/ai_assistant/catalog/controller/api.php` take a named lock per key (`GET_LOCK`, 10 s, `503` when not obtained) before the lookup in every write and release it after commit or rollback, per research R10; update the extension's contract text in `specs/001-test-shop/contracts/catalog-endpoint.md` (Rules: writes to the same key are serialized)
- [X] T033 Integration test in `tests/ShopLoader.IntegrationTests/ShopClientTests.cs`: two simultaneous upserts of one new SKU through the library leave exactly one product with that SKU (SC-007)
- [X] T034 Update `README.md`/`CLAUDE.md` only if they describe client failure handling; run `dotnet format`, the unit suites, the integration suite and `tools/smoke-test.sh`; tick `quickstart.md` scenario for retries

**Checkpoint**: SC-007 and SC-008 hold.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on T001; blocks all stories
- **US1 (Phase 3)**: depends on Phase 2; the loader build is red until T012–T014 are done
- **US2 (Phase 4)**: depends on Phase 2 and on US1 for the integration test that runs through the stack (T019 only needs Phase 2)
- **US3 (Phase 5)**: depends on Phase 2; T024 should run after US1
- **Polish (Phase 6)**: depends on all stories
- **Resilience (Phase 7)**: depends on Phases 3 and 4; T029 before T030; T031 with T029; T032 before T033

### Within Each Story

- T005–T008 in parallel, then T009, then T010
- US1: T012, then T013 and T014, then T015 and T016, then T017; T011 may be written first
- US2: T018 and T019 in parallel, then T020, then T021 and T022

### Parallel Opportunities

- T002 and T004 with T001's follow-ups; T005–T008 together
- T011 in parallel with T012
- T018 and T019 together; T023 with any US2 task

## Parallel Example: Foundational

```text
Task: "Create CatalogCategory, CatalogProduct, UpsertResult in src/ShopClient/"
Task: "Create Exceptions.cs in src/ShopClient/"
Task: "Create IShopClient.cs and ICatalogLoadingClient.cs in src/ShopClient/"
Task: "Create ShopType.cs and ShopConnection.cs in src/ShopClient/"
```

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 and Phase 2 (library builds alone)
2. Phase 3: switch the loader; run the existing suites and the demo
3. **Stop and validate**: nothing changed for the loader
4. Then US2 (tests and neutrality check), US3 (docs and independence), Polish

### Notes

- Existing loader test expectations are the regression net: never edit them to make them pass
- Move files with `git mv` so history follows
- Commit after each task or logical group; do not push until the PR is ready (push sparingly: every push runs CI). Open the pull request, with a short description and a link to the spec, only when asked
