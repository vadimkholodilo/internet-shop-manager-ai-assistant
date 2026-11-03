# Internet Shop Manager AI Assistant Constitution

## Core Principles

### I. .NET-First Backend

All primary backend logic MUST be written in .NET. Python MAY be used only for AI-specific
components where .NET support is materially worse, MUST be isolated behind a clear service
boundary, and each such use MUST be justified in the implementation plan. Embedding generation
and search default to .NET and MAY move to Python only if the plan shows a concrete reason.

Rationale: one primary backend stack keeps the system maintainable; Python is an exception for
ecosystem gaps, not a parallel stack.

### II. Integrate, Never Replace

The assistant MUST integrate with an existing shop strictly through that shop's official API.
Direct access to the shop's database is forbidden, including for loading test data. The project
MUST NOT build its own shop.

The assistant is not specific to any shop. Every operation on a shop MUST go through a shop
connector that implements a shop-neutral contract, and only the connector MAY contain
shop-specific code or call the shop's API. Integrating a new shop MUST require only a new
connector, with no changes to the assistant or to other consumers of the contract. This applies
even when the current shop alone would not justify the abstraction.

Rationale: real businesses already have a shop and will not replace it; API-only integration
keeps the assistant portable and the shop safe, and a connector boundary keeps the assistant
extensible to other shops.

### III. Grounded and Safe Assistant (NON-NEGOTIABLE)

The assistant MUST:
- offer only products that exist in the shop's catalog and state only facts supported by
  catalog data;
- never create an order without explicit customer confirmation of a shown summary;
- never request, accept, store, or repeat payment credentials; payment happens only through a
  payment link or QR code on the payment provider's side;
- hand the conversation to a human when it is not confident or when the customer asks for one.

These rules MUST NOT be weakened by any feature, prompt change, or deadline.

Rationale: wrong products, unwanted orders, and payment data exposure cause direct customer and
business harm.

### IV. Human in the Loop

Shop managers MUST be able to view all conversations and join any of them at any time. The
assistant supports managers and MUST NOT be designed or described as replacing them.

Rationale: the goal is to free managers from routine work, so they can focus on cases that need
a person.

### V. Test-First with AI Evaluations

Automated tests are mandatory. AI behaviour MUST be covered by evaluation tests that account for
LLM non-determinism, such as repeated runs and assertions on invariants instead of exact text.
Each safety rule in Principle III MUST have at least one automated test. Tests SHOULD be written
before the implementation they cover.

Rationale: manual testing cannot cover all scenarios, and model output varies between runs.

### VI. Language Policy

All specifications, documentation, code, comments, and developer discussion MUST be in English.
All customer-facing conversations MUST be in Russian only; other languages are not supported.

Rationale: English is the working language of the development team; the shop and its customers
are Russian-speaking.

### VII. Incremental Simplicity

Work MUST be delivered in small, independently testable slices, starting with the core
capability. Features not required by the current slice MUST NOT be built (YAGNI), and added
complexity MUST be justified in the plan.

Rationale: a pet project with a public build-in-the-open series progresses best in small,
demonstrable steps.

### VIII. Infrastructure as Code

All servers and the software running on them MUST be provisioned and configured through
version-controlled infrastructure-as-code. Manual changes on servers are forbidden except for
emergency fixes, which MUST be captured in code afterwards. Environments MUST be reproducible
from the repository, and secrets MUST NOT be committed. The choice of IaC tooling is made in the
implementation plan.

Rationale: publicly hosted environments must be rebuildable, reviewable, and free of undocumented
manual state; this also supports the shop's reset and re-seed requirement.

### IX. Containerized Environments with Development Overrides

Everything MUST run in containers: the application services, the shop, the database and any other
external service. The whole environment MUST start with a single documented command. A separate
development override configuration MUST expose the ports of all external services (for example
the shop and its database) on the local machine, so that the services under development can run
outside containers against them. Development overrides MUST NOT be required for, or change the
behaviour of, the default configuration.

Rationale: containers make the project easy for blog readers to start and keep environments
reproducible, while port exposure keeps day-to-day development fast.

## Technology and Integration Constraints

- Backend stack is governed by Principle I; shop integration by Principle II.
- Test data MUST be loaded into the test shop through its API by a separate, repeatable script
  from a one-time snapshot of a public catalog, and MUST NOT be used for resale or competition.
- Out of scope until a new specification adds them: voice messages, returns and refunds,
  manager analytics, telephony.

## Development Workflow

- Every feature starts from a specification in English under `specs/`, followed by a plan and
  tasks.
- Plans MUST include a Constitution Check against the principles above, and any violation MUST be
  explicitly justified.
- Every feature MUST be merged to the main branch through a pull request. The pull request MUST
  have a short description of what the feature is about and link to its spec.
- A GitHub Actions workflow MUST run the automated test suite, including AI evaluation tests, on
  every pull request. The pull request MUST NOT be merged until that check passes, and the main
  branch MUST be protected to enforce this.
- The workflow and the branch protection settings are part of the repository's delivery process
  and MUST be kept in version control where GitHub allows it.

## Governance

This constitution supersedes other practices. Amendments MUST be documented in this file with an
updated version and date, and MUST state any migration needed for existing specs and plans.
Versioning follows semantic versioning: MAJOR for removed or redefined principles, MINOR for
added principles or materially expanded guidance, PATCH for clarifications and wording. Plans and
reviews MUST verify compliance with the principles, with Principle III checked on every change
that touches assistant behaviour.

**Version**: 1.4.0 | **Ratified**: 2026-11-02 | **Last Amended**: 2026-11-03
