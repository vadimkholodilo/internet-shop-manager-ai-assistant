# Feature Specification: Test Electronics Shop

**Feature Branch**: `001-test-shop`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Test electronics shop for development and demos of the AI shop assistant. A working, publicly reachable Russian-language test internet shop that sells electronics, so that people and the assistant can browse a catalog, place orders and see order statuses."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse a realistic electronics catalog (Priority: P1)

A visitor opens the test shop in a browser and sees a Russian-language electronics store with categories and products. Each product has a name, brand, photo, price in rubles, key attributes and availability. The catalog looks like a real electronics retailer's, with enough variety (categories, brands, price ranges) to test product-selection conversations.

**Why this priority**: Without a populated, reachable shop, nothing else, including the AI assistant, can be built or tested.

**Independent Test**: Open the public address, navigate through categories, open product pages, and verify that products are in Russian, priced in rubles, have photos and show availability.

**Acceptance Scenarios**:

1. **Given** the shop is deployed and seeded, **When** a visitor opens its public address, **Then** they see a Russian-language storefront with electronics categories.
2. **Given** a category, **When** the visitor opens it, **Then** they see products with name, photo, price in rubles and availability.
3. **Given** a product page, **When** the visitor opens it, **Then** they see the brand, attributes and description from the seeded data.
4. **Given** the seeded catalog, **When** it is inspected, **Then** it spans multiple categories, multiple brands per category and a wide price range.

---

### User Story 2 - Seed the shop from a data snapshot through its API (Priority: P1)

A developer runs a single loader script that reads the product snapshot file and fills the shop with categories, products, images, prices and stock, using only the shop's official programmatic interface. Running the script again does not create duplicates: existing items are updated and new items are added.

**Why this priority**: The catalog must be reproducible. It also proves that everything the assistant later needs can be done through the official interface.

**Independent Test**: Start from an empty shop, run the loader, count items; run it again and verify the counts are unchanged.

**Acceptance Scenarios**:

1. **Given** an empty shop and a valid snapshot file, **When** the loader runs, **Then** all categories, products, images, prices and stock levels from the file appear in the shop.
2. **Given** an already seeded shop, **When** the loader runs again with the same file, **Then** no duplicate categories or products are created.
3. **Given** a snapshot where one product's price changed, **When** the loader runs, **Then** that product's price is updated and no other product changes.
4. **Given** a malformed record in the snapshot, **When** the loader runs, **Then** it reports which record failed and continues with the rest.
5. **Given** the loader, **When** it runs, **Then** it never reads or writes the shop's database directly.

---

### User Story 3 - External service reads the catalog and manages orders via API (Priority: P1)

An external service, authenticated with its own credentials, can read the catalog, create orders for a customer, read an order's current status, and detect status changes. This is what the AI assistant will use.

**Why this priority**: The assistant depends on exactly these capabilities, so the shop is not usable for the main project without them.

**Independent Test**: Using only the service credentials, list products, create an order, read its status, change the status as an administrator, and read the new status.

**Acceptance Scenarios**:

1. **Given** valid service credentials, **When** the service requests the catalog, **Then** it receives products with name, category, brand, attributes, price, photo and availability.
2. **Given** valid service credentials, **When** the service creates an order with customer details, items and delivery method, **Then** the order exists in the shop with those details and a unique identifier.
3. **Given** an existing order, **When** the service asks for its status, **Then** it receives the current status.
4. **Given** an order whose status was changed by an administrator, **When** the service checks it, **Then** it can detect the change and read the new status.
5. **Given** missing or invalid credentials, **When** a request is made, **Then** it is rejected and no data is exposed or changed.

---

### User Story 4 - Administrator changes order status and gets a stub payment link (Priority: P2)

A shop administrator can see orders and move them through statuses (for example processing, shipped, delivered, cancelled) so that status-change flows can be tested. After an order is placed, a test payment method produces a payment link or QR code. Nothing is actually charged.

**Why this priority**: Needed for end-to-end tests of order tracking and payment flows, but works only once ordering exists.

**Independent Test**: Place an order, open the administration area, change its status, and open the payment link to see a test payment page.

**Acceptance Scenarios**:

1. **Given** an order exists, **When** an administrator changes its status, **Then** the new status is saved and visible through the programmatic interface.
2. **Given** an order is placed, **When** the test payment method is used, **Then** a payment link or QR code is available for that order.
3. **Given** a test payment page, **When** a visitor completes it, **Then** no real money is charged and the order can be marked as paid.

---

### User Story 5 - Reset and re-seed to a clean state (Priority: P3)

A developer can reset the shop to a clean state (no products, no orders) and re-seed it, so tests start from a known baseline.

**Why this priority**: Makes automated tests and demos repeatable; manual workarounds exist if missing.

**Independent Test**: Create orders and edit products, run the reset, verify the shop is empty, then run the loader and verify the baseline catalog.

**Acceptance Scenarios**:

1. **Given** a shop with orders and changes, **When** the reset is run, **Then** all products and orders are removed and the shop settings and service credentials remain usable.
2. **Given** a reset shop, **When** the loader is run, **Then** the baseline catalog is restored.

---

### Edge Cases

- A product in the snapshot has no photo or no price; the loader skips or flags it and reports it.
- The snapshot contains the same product twice; only one product is created.
- Product names or attributes contain special characters, Cyrillic text or very long values; they are stored and displayed correctly.
- The shop's programmatic interface is temporarily unavailable during seeding; the loader fails clearly and can be re-run safely.
- An order is requested for a product that is out of stock; the programmatic interface reports it instead of silently creating the order.
- Two orders are created at the same time; each gets a distinct identifier.
- The service credentials are revoked; subsequent requests are rejected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shop MUST be reachable at a public test address and present a Russian-language storefront with prices in rubles.
- **FR-002**: The shop MUST present an electronics catalog with categories, brands, attributes, photos, prices and availability.
- **FR-003**: A one-time snapshot of product data from a real electronics retailer MUST be stored as a file in a documented format containing name, category, brand, attributes, price, photo and availability for each product.
- **FR-004**: The snapshot MUST be used for testing only and MUST NOT be redistributed for resale or competition.
- **FR-005**: A separate loader MUST create categories, products, images, prices and stock in the shop from the snapshot.
- **FR-006**: The loader MUST use only the shop's official programmatic interface and MUST NOT access the shop's database directly.
- **FR-007**: The loader MUST be idempotent: re-running it MUST NOT create duplicate categories or products and MUST apply changed values.
- **FR-008**: The loader MUST report records it could not load and continue with the remaining records.
- **FR-009**: The shop's programmatic interface MUST let an authorised external service read the catalog with all fields listed in FR-002.
- **FR-010**: The shop's programmatic interface MUST let an authorised external service create an order with customer name, phone, delivery address, delivery method and items.
- **FR-011**: The shop's programmatic interface MUST let an authorised external service read an order's current status and detect status changes.
- **FR-012**: Requests without valid service credentials MUST be rejected without exposing or changing data.
- **FR-013**: An administrator MUST be able to view orders and change their status through the shop's administration area.
- **FR-014**: The shop MUST provide a test payment method that produces a payment link or QR code per order and never charges real money.
- **FR-015**: The shop MUST be resettable to a clean state without redeploying it, and re-seedable afterwards.
- **FR-016**: Setup and operation (deploying, seeding, resetting, obtaining service credentials) MUST be documented in English.

### Key Entities

- **Product Snapshot**: A file of source product records (name, category, brand, attributes, price, photo, availability) with its source and capture date.
- **Category**: A grouping of products in the shop's catalog.
- **Product**: A catalog item with brand, attributes, photo, price in rubles and stock/availability.
- **Order**: A purchase with customer details, items, delivery method, address, status and payment link or QR code.
- **Order Status**: A stage of an order's life (for example new, processing, shipped, delivered, cancelled).
- **Service Credential**: Authorisation given to an external service, such as the assistant, to use the programmatic interface.
- **Administrator**: A shop user who manages orders and statuses.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A visitor can open the public address and see a Russian-language product listing within 5 seconds.
- **SC-002**: The seeded catalog contains at least 300 products across at least 8 categories and at least 15 brands, with at least 95% of products having a photo and a price.
- **SC-003**: Seeding an empty shop completes in under 30 minutes, and a second run on the seeded shop creates 0 duplicates.
- **SC-004**: Using only service credentials, a developer can list products, create an order and read its status in under 5 minutes of effort following the documentation.
- **SC-005**: A status change made by an administrator is visible through the programmatic interface within 10 seconds.
- **SC-006**: 100% of requests without valid credentials are rejected.
- **SC-007**: A full reset and re-seed returns the shop to the baseline in under 45 minutes with no manual steps beyond running documented commands.

## Assumptions

- The shop is built on OpenCart, and the shop's own administration area is used for order management; no custom shop software is written.
- The shop's official programmatic interface can cover catalog read, order creation and status read. If it cannot, how to close the gap without direct database access is a planning decision.
- A test server is available to host the shop publicly; hosting provider and domain are decided in planning.
- The catalog is electronics only, in Russian, with prices in rubles.
- The product snapshot is collected once from a real electronics retailer's public catalog for testing only; scraping is a one-off step, not part of the running system.
- Payments are stubbed: no payment provider is integrated and no real money moves.
- Out of scope: real payments, real delivery integrations, production hardening, and the AI assistant itself (feature `002-shop-ai-assistant`, which depends on this feature).
