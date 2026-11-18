# internet-shop-manager-ai-assistant

AI assistant that helps customers of an existing internet shop find products, order and track orders
in chat, with shop managers supervising and taking over. See `README.md` for the overview and roadmap.

All project rules (stack, shop integration, safety, language, IaC, testing, PR workflow) are in
`.specify/memory/constitution.md`, which overrides other practices. Read it before planning or coding.

## Workflow

Spec Kit, one feature per directory in `specs/`, one feature at a time. Features so far: `001-test-shop`
and `002-shared-shop-client` (the shop client library in `src/ShopClient`); the AI assistant feature
comes next. The required PR check is defined in `.github/workflows/pull-request.yml`.
