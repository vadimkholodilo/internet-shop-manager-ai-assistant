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
- Assistant safety rules (catalog-only products, explicit order confirmation, no payment credentials,
  human handoff) are non-negotiable and each needs an automated test.

## Workflow

Spec Kit, one feature per directory in `specs/` (`001-test-shop`, `002-shop-ai-assistant`). Feature 002
depends on 001.
