# Quickstart: Shared Shop Client Library

Validation scenarios for this feature. Prerequisites: .NET 10 SDK and Docker (for the integration tests and the demo).

## 1. Nothing changed for the loader (User Story 1)

```bash
dotnet build && dotnet test tests/ShopLoader.UnitTests && dotnet test tests/ShopLoader.IntegrationTests
tools/smoke-test.sh          # docker compose up -d, checks the seeded shop
```

Expected: all pass with no changed expectations; the demo shop has the same categories, products and images as before.

Optional flag check:

```bash
dotnet run --project src/ShopLoader -- --file data/sample-catalog.json --url http://localhost:8080 --api-key local-demo-api-key-change-me --shop opencart
dotnet run --project src/ShopLoader -- --file data/sample-catalog.json --url http://localhost:8080 --api-key x --shop nope   # exit 2, lists supported types
```

## 2. The contract is shop-neutral (User Story 2)

```bash
grep -rniE "opencart|catalog-api" src/ShopLoader --include=*.cs   # no matches
dotnet test tests/ShopClient.UnitTests                              # factory, shop type, failure mapping
dotnet test tests/ShopLoader.IntegrationTests --filter ShopClientTests
```

Expected: the grep prints nothing; the library's public types in [contracts/shop-client-api.md](contracts/shop-client-api.md) contain no OpenCart names.

## 3. Reuse without the loader (User Story 3)

```bash
grep -c ShopLoader src/ShopClient/ShopClient.csproj   # 0
dotnet build src/ShopClient
```

Expected: the library builds alone; `ShopClientTests` upserts a category, a product and an image through the factory without calling loader code. Follow `src/ShopClient/README.md` "Adding a shop" to check it is understandable on its own.
