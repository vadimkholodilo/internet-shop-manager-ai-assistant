# Research: Shared Shop Client Library

Decisions that the spec left to the plan. No unresolved clarifications.

## R1. Library name and location

**Decision**: `src/ShopClient/ShopClient.csproj`, namespace `ShopClient`, OpenCart code in `ShopClient.OpenCart` (internal).
**Rationale**: matches the existing `src/ShopLoader` layout; "ShopClient" describes both current consumers (the loader now, the connector later) without naming the loader or OpenCart.
**Alternatives**: `ShopConnector.Client` rejected because it would collide with the future connector project name; a `ShopLoader.Client` namespace rejected because the library must not read as loader-owned.

## R2. Shape of the contracts

**Decision**: `IShopClient` (general; `WaitUntilReadyAsync`, `IDisposable`) and `ICatalogLoadingClient : IShopClient` (`UpsertCategoryAsync`, `UpsertProductAsync`, `UploadImageAsync`). Image upload takes a local file path (spec clarification). Every async method takes `CancellationToken cancellationToken = default`, so callers need not pass `CancellationToken.None`.
**Rationale**: the loading contract extends the general one, so a loader can wait for readiness through the same object; a read-only consumer references only `IShopClient`. A shop client may implement `IShopClient` alone.
**Alternatives**: two unrelated interfaces rejected because the loader would need two references to one object; one interface rejected by the spec clarification.

## R3. Shop-neutral types

**Decision**: `CatalogCategory` and `CatalogProduct` records in the library with the fields the loader sends today; the loader maps `CategoryRecord`/`ProductRecord` to them. `UpsertResult` moves to the library and is public.
**Rationale**: spec FR-006: the library must not depend on the product file format. The mapping is a few lines per type, and a file format change then never touches the library.
**Alternatives**: moving `ProductRecord` into the library rejected because it embeds file rules (image path relative to an images directory, file validation).

## R4. Factory and shop type selection

**Decision**: `ShopClientFactory.CreateShopClient(ShopType, ShopConnection)` and `CreateCatalogLoadingClient(...)`, plus `ShopType.TryParse(string, out ShopType)` and `ShopType.SupportedNames`. The loader adds `--shop <type>` (default `opencart`); an unknown value is a usage error (exit 2) whose message lists `ShopType.SupportedNames`. Asking for a capability the shop's client lacks throws `UnsupportedShopException`.
**Rationale**: spec FR-007a/b. `ShopType` is a small hand-written type with a name table instead of an `enum` with `Enum.Parse`, to stay reflection-free and trim safe, and so supported names come from one place.
**Alternatives**: dependency injection container rejected (new dependency, reflection, no second implementation yet); a plugin/assembly-scanning registry rejected (reflection, not AOT safe).

## R5. Native AOT compatibility

**Decision**: the library sets `IsAotCompatible`, trim and AOT analyzers and `JsonSerializerIsReflectionEnabledByDefault=false`, the same as the loader. The source-generated JSON context moves with the OpenCart client and keeps its snake_case naming; the loader keeps no JSON context.
**Rationale**: the loader is published with `PublishAot`, and a library that is not trim safe would break that. `TreatWarningsAsErrors` (already in `Directory.Build.props`) enforces it at build.
**Alternatives**: none worth considering.

## R6. Where each test lives (Principle V: cover once)

**Decision**:
- `OpenCartCatalogClientTests` (all its real-socket scenarios) moves to `tests/ShopClient.UnitTests`, using the renamed client.
- New unit tests there: the factory returns the OpenCart client for its type, rejects an unknown type with the list of supported names, and `ShopType` parsing.
- New `ShopClientTests` in `ShopLoader.IntegrationTests` upserts a category, a product and an image through the factory and the contracts against the real shop, and checks created versus updated and a rejected record (a wrong API key is already covered by `LoaderTests`, so it is not repeated). It uses the existing `ShopTest` helpers and `CatalogApi` for verification, adding no contract details to test classes.
- Existing `LoaderTests`, `CatalogApiTests` and `SampleDemoTests` stay as they are: they cover the loader and the extension endpoint, not the client's code. The `--shop` flag gets a small unit test in `CliTests`.
- No new test project for integration: the container stack (`ShopStack`) is shared and duplicating it would cost more than it proves.
**Rationale**: spec FR-012 and SC-006. The throwaway "second consumer" of Story 3 is `ShopClientTests` plus the library project having no reference to the loader, which a build check proves.

## R7. Build, image and tooling impact

**Decision**:
- `src/ShopLoader/Dockerfile` copies `src/ShopClient/` before publishing; build context stays the repository root.
- `InternetShop.slnx` gains `src/ShopClient` and `tests/ShopClient.UnitTests`.
- `.github/workflows/pull-request.yml` runs the new unit test project next to the existing ones.
- `tools/check-changed.sh` needs no change (Dockerfile path is unchanged).
**Rationale**: keeps the one-command demo and the required `tests` check working.

## R8. Documentation

**Decision**: `src/ShopClient/README.md` covers purpose, the two contracts, who uses them, and "adding a shop" (new folder, implement the contract, register in `ShopType` and the factory, test through `ShopClientTests`). `README.md`, `CLAUDE.md` and `tests/README.md` are updated for the new structure.
