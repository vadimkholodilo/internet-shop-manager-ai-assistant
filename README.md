# internet-shop-manager-ai-assistant

An AI assistant for internet shops. It helps customers choose products, place orders and track them
in a chat, while shop managers supervise conversations and step in at any time.

The assistant **supports** shop managers and does not replace them. It takes over routine product-selection
questions so managers can spend time on complaints, returns, suppliers, the catalog and loyal customers.

This is a pet project built in the open, with a series of posts in a Telegram channel (in Russian).

## How it works

- Integrates with an **existing** shop strictly through the shop's official API. It never touches the
  shop's database and does not build its own shop.
- Customers write in free-form Russian text, first in Telegram and later in a website chat, with one
  shared conversation history.
- The assistant only offers products that exist in the catalog, never creates an order without explicit
  confirmation, never asks for payment details, and hands off to a human when unsure.

## Roadmap

Development is spec-driven ([Spec Kit](https://github.com/github/spec-kit)). Each feature lives in `specs/`.

| # | Feature | Status | Depends on |
|---|---------|--------|------------|
| 001 | [Test electronics shop](specs/001-test-shop/spec.md): OpenCart in Docker, JSON product loader, generated sample catalog | Implemented | none |

Next, once 001 is done: the shop connector, public address, order API and a larger catalog, then the shop AI assistant (product search, ordering, order tracking, handoff, ratings).
Features are specified one at a time.

Not planned yet: voice messages, returns, manager analytics, telephony.

## Try the test shop

The test shop is an OpenCart electronics store in Russian, seeded with a generated sample catalog. The only
prerequisite is Docker with the Compose plugin.

```bash
cp .env.example .env      # optional, the defaults work as they are
docker-compose up -d
```

`up -d` returns once the shop is healthy; the loader then fills it in about 10 seconds
(`docker-compose logs -f loader`, it exits with code 0 when done). Then open <http://localhost:8080>.
Running the command again changes nothing: the loader updates existing items and never duplicates them.

| What | Where |
|------|-------|
| Storefront | <http://localhost:8080> (change the port with `SHOP_PORT` in `.env`) |
| Administration | <http://localhost:8080/admin> (`admin` / `admin` by default, see `.env`) |
| Catalog API | `http://localhost:8080/catalog-api/...`, `Authorization: Bearer <SHOP_API_KEY>` |

If the port is already taken, Docker reports `port is already allocated`; set another `SHOP_PORT` in `.env`.
Stop with `docker-compose down`, or `docker-compose down -v` to also delete the shop's data.

**Loading your own catalog.** Put a product file (format in [`data/README.md`](data/README.md)) and its images
anywhere and run the loader against the running shop:

```bash
dotnet run --project src/ShopLoader -- --file my-catalog.json --url http://localhost:8080 --api-key local-demo-api-key-change-me
```

**Development.** The development override also publishes the database port, so tools can run on your machine:

```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d   # shop :8080, database :3306
```

The loader is a Native AOT .NET 10 program; `dotnet run` works with the .NET 10 SDK, and the Docker build
produces the AOT binary. Tests: `dotnet test` runs unit tests and integration tests, which start real OpenCart
and MariaDB containers with Testcontainers (Docker required, the first run builds the images).
`tools/smoke-test.sh` runs the real `docker compose up -d` demo and checks the result.

How it is built is described in [`specs/001-test-shop/`](specs/001-test-shop/plan.md).

## Principles

The binding rules are in [`.specify/memory/constitution.md`](.specify/memory/constitution.md). In short:
.NET-first backend (Python only where AI tooling is materially better), API-only shop integration, a safe
and grounded assistant, humans in the loop, tested AI behavior, small incremental slices, and
infrastructure managed only as code.

## Language policy

- Specs, docs, code, comments and developer discussion: **English**.
- Customer-facing conversations and the test shop content: **Russian** only.

## License

See [LICENSE](LICENSE).
