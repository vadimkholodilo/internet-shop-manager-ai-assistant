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
| 001 | [Test electronics shop](specs/001-test-shop/spec.md): OpenCart shop, product snapshot, API loader, order API | Specified | none |
| 002 | [Shop AI assistant](specs/002-shop-ai-assistant/spec.md): product search, ordering, order tracking, handoff, ratings | Specified | 001 |

Not planned yet: voice messages, returns, manager analytics, telephony.

## Principles

The binding rules are in [`.specify/memory/constitution.md`](.specify/memory/constitution.md). In short:
.NET-first backend (Python only where AI tooling is materially better), API-only shop integration, a safe
and grounded assistant, humans in the loop, tested AI behavior, and small incremental slices.

## Language policy

- Specs, docs, code, comments and developer discussion: **English**.
- Customer-facing conversations and the test shop content: **Russian** only.

## License

See [LICENSE](LICENSE).
