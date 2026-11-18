# Quickstart: Test Electronics Shop (Local Demo)

Validation scenarios for this feature. `tools/smoke-test.sh` runs scenario 1 end to end.

## Prerequisites

Docker with the Compose plugin. For development and tests: .NET 10 SDK (Native AOT publishing also needs clang and zlib headers on the host; the Docker build has them).

## 1. One-command demo (User Stories 1-3)

```bash
cp .env.example .env        # optional; local defaults work without it
docker-compose up -d
```

Wait until the loader container has exited with code 0 (`docker-compose ps -a`, about 10 seconds after `up -d`
returns). Open the local address (`SHOP_PORT` in `.env.example`, default `http://localhost:8080`) and check: Russian storefront, at least 3
categories, at least 20 products with photos and prices in rubles.

Re-run `docker-compose up -d`: no duplicates, shop still works.

## 2. Idempotency and updates (User Story 2)

Change one `price` in `data/sample-catalog.json` and run the loader again
(`dotnet run --project src/ShopLoader -- --file data/sample-catalog.json --url http://localhost:8080 --api-key local-demo-api-key-change-me`).
Only that price changes; product count is unchanged.

## 3. Development override (User Story 4)

```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d
dotnet run --project src/ShopLoader -- --file data/sample-catalog.json --url http://localhost:8080 --api-key local-demo-api-key-change-me
```

The shop and database are reachable on the documented host ports. Without the override, the database port
is not exposed.

## 4. Automated tests (User Story 5)

```bash
dotnet test
```

Unit tests run first; the integration tests start real OpenCart and MariaDB containers through
Testcontainers, load the sample catalog, and check loading, idempotency, price updates and malformed
records. No containers remain afterwards.
