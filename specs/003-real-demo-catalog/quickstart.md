# Quickstart: Real Demo Catalog

Prerequisites: .NET 10 SDK, Docker, internet access. Run from the repository root.

## 1. Build the subset (User Story 1)

```bash
dotnet run tools/build-catalog/build-catalog.cs -- --limit 100 --out out/subset
```

Expected: exit code 0; summary shows 100 products, ≥5 subjects, ≥10 brands, some out of stock; `out/subset` under
10 MB. Takes under 15 minutes with a cold cache.

## 2. Re-run is cached and deterministic

```bash
cp out/subset/catalog.json /tmp/first.json
dotnet run tools/build-catalog/build-catalog.cs -- --limit 100 --out out/subset
cmp /tmp/first.json out/subset/catalog.json
```

Expected: the summary reports 0 network requests to Wildberries for cards and photos; the files are identical.

## 3. Replace the sample and run the tests (User Story 2)

Copy the subset as shown in [contracts/build-catalog-cli.md](contracts/build-catalog-cli.md), then:

```bash
dotnet test
tools/smoke-test.sh
```

Expected: all tests pass; the smoke test reports 100 products, a Russian storefront and an idempotent re-run. No test
or script mentions Norda or `PH-001`.

## 4. Full set (optional)

```bash
dotnet run tools/build-catalog/build-catalog.cs -- --dumps 9-12 --out out/full --archive
```

Expected: a few thousand products and `out/full/catalog.tar.gz`, which loads with the existing loader:
`--file out/full/catalog.json --images-dir out/full/images`.
