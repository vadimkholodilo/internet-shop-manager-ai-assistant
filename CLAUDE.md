# internet-shop-manager-ai-assistant

AI assistant that helps customers of an existing internet shop find products, order and track orders
in chat, with shop managers supervising and taking over. See `README.md` for the overview and roadmap.

## Rules

- Follow `.specify/memory/constitution.md`; it overrides other practices.
- All specs, docs, code, comments and discussion are in English. Customer-facing conversations and the
  test shop content are in Russian only.
- Backend is .NET. Python only for AI-specific parts where .NET support is materially worse, behind a
  service boundary and justified in the plan.
- Integrate with the shop only through its API, never its database.
- Servers and software are managed only through version-controlled infrastructure as code; no manual
  server changes, no committed secrets.
- Assistant safety rules (catalog-only products, explicit order confirmation, no payment credentials,
  human handoff) are non-negotiable and each needs an automated test.

## Workflow

Spec Kit, one feature per directory in `specs/`, one feature at a time. Features so far: `001-test-shop` and `002-shared-shop-client` (the shop client library in `src/ShopClient`, used by the loader and by the future connector);
the AI assistant feature is added after them.

Every feature is merged through a pull request with a short description; the `tests` GitHub Actions check
must pass first (`.github/workflows/pull-request.yml`).
