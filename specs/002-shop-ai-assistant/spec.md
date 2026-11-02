# Feature Specification: Internet Shop AI Assistant

**Feature Branch**: `002-shop-ai-assistant`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "AI assistant for an existing internet shop (test shop: electronics). Customers chat in free text and get help finding products, placing orders and tracking orders. Shop managers supervise conversations and take over when needed."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find products by free-text description (Priority: P1)

A customer describes what they want in their own words (for example, "inexpensive wireless running headphones, up to 5000 rubles"). The assistant understands the request, searches the shop's real catalog and presents a few matching products with photo, price and availability. If the request is too vague it asks one or two short clarifying questions. If nothing fits, it says so honestly and suggests the closest alternatives.

**Why this priority**: Product selection is where managers spend most of their time and is the foundation for ordering. Delivers value on its own, with no ordering needed.

**Independent Test**: Send a set of free-text requests against a test catalog and verify that every product shown exists in the catalog, matches the stated constraints, and is shown with photo, price and availability.

**Acceptance Scenarios**:

1. **Given** a catalog with matching products, **When** the customer writes "inexpensive wireless running headphones, up to 5000 rubles", **Then** the assistant shows several in-catalog products that are wireless headphones priced at or below 5000 rubles, each with photo, price and availability.
2. **Given** a vague request such as "I need a phone", **When** the assistant lacks key details, **Then** it asks at most two short clarifying questions (for example budget and purpose) before or while showing options.
3. **Given** a request that no catalog product satisfies, **When** the assistant searches, **Then** it states plainly that no exact match exists and offers the closest in-catalog alternatives, without inventing products.
4. **Given** any request, **When** the assistant replies, **Then** it never mentions a product, price or specification that is not present in the catalog.

---

### User Story 2 - Place an order in the chat (Priority: P2)

After choosing a product, the customer says they want to buy it. The assistant collects the required details (name, phone, delivery address, delivery method), shows a summary (item, quantity, price, total) and asks for explicit confirmation. Only after the customer confirms does it create the order in the shop's system and give the customer a payment link or QR code.

**Why this priority**: Turns selection into revenue, but depends on product discovery (P1).

**Independent Test**: Walk through a purchase with a known product and verify that no order exists before confirmation, exactly one order exists after confirmation with the correct details, and a payment link or QR code is returned.

**Acceptance Scenarios**:

1. **Given** the customer has chosen a product and says "I'll take it", **When** the assistant needs more information, **Then** it asks for the missing name, phone, address and delivery method.
2. **Given** all details are collected, **When** the assistant is ready to proceed, **Then** it shows a summary of what, how many and at what price, and asks for explicit confirmation without yet creating an order.
3. **Given** the customer has not confirmed, **When** the conversation ends or the customer changes their mind, **Then** no order is created in the shop.
4. **Given** the customer explicitly confirms, **When** the assistant creates the order, **Then** the order exists in the shop's system with the confirmed details and the customer receives a payment link or QR code.
5. **Given** any point in the conversation, **When** the customer offers card numbers or other payment details, **Then** the assistant does not request, store or repeat them and directs the customer to pay via the link or QR code.

---

### User Story 3 - Track an order (Priority: P3)

A customer asks "Where is my order?". The assistant finds their order and reports its current stage. When the order's status later changes, the assistant proactively messages the customer.

**Why this priority**: Reduces repetitive status questions to managers; relies on orders existing (P2).

**Independent Test**: Create an order, ask for its status, then change the status in the shop and verify the customer is notified.

**Acceptance Scenarios**:

1. **Given** the customer has an order, **When** they ask where it is, **Then** the assistant replies with the order's current status.
2. **Given** the customer has several orders, **When** they ask about "my order", **Then** the assistant asks which one they mean or lists them.
3. **Given** an order's status changes in the shop, **When** the change is detected, **Then** the customer receives a message about the new status in the channel they use.
4. **Given** the assistant cannot find a matching order, **When** the customer asks, **Then** it says so and offers to hand off to a manager.

---

### User Story 4 - Chat from any channel with continuous history (Priority: P2)

A customer can talk to the assistant in a messenger (Telegram first) or in the chat on the shop's website. The conversation history is shared: a conversation started in one channel can be continued in another and the assistant remembers it.

**Why this priority**: Meets customers where they are; needed for a realistic deployment, but the core capabilities work in a single channel first.

**Independent Test**: Start a conversation in the messenger, continue it in the website chat as the same customer, and verify that the assistant uses earlier context.

**Acceptance Scenarios**:

1. **Given** a customer discussed products in the messenger, **When** they open the website chat as the same customer, **Then** the assistant refers to the earlier conversation correctly.
2. **Given** the customer is recognised in both channels, **When** a status update is due, **Then** it is delivered in the channel the customer used most recently.

---

### User Story 5 - Manager oversight and handoff (Priority: P2)

A shop manager can see all conversations and join any of them at any time. The assistant hands the conversation to a manager when it is unsure or when the customer asks for a human, rather than guessing.

**Why this priority**: Core to the "assistant, not replacement" promise and protects customers from wrong answers.

**Independent Test**: Ask for a human, and ask something outside the assistant's knowledge; verify the conversation is flagged for a manager, and that a manager can read the history and reply.

**Acceptance Scenarios**:

1. **Given** the customer writes "connect me to a person", **When** the assistant receives it, **Then** it tells the customer a manager will take over and flags the conversation for a manager.
2. **Given** a question the assistant cannot answer confidently (for example a complaint or return), **When** it responds, **Then** it hands off instead of improvising.
3. **Given** a manager joins a conversation, **When** they send a message, **Then** the customer receives it in their channel and the assistant stops replying until handed back.
4. **Given** any conversation, **When** a manager opens it, **Then** they see the full history including earlier channels.

---

### User Story 6 - Rate assistant answers (Priority: P3)

Every assistant answer offers like and dislike controls. A like records that the answer was good and marks it as a candidate for a future training set. A dislike opens a field where the customer can say what was wrong.

**Why this priority**: Gives the team quality feedback when it cannot read every conversation.

**Independent Test**: Like one answer and dislike another with a comment; verify both are stored with the answer and conversation they refer to.

**Acceptance Scenarios**:

1. **Given** an assistant answer, **When** the customer presses like, **Then** the rating is recorded against that answer.
2. **Given** an assistant answer, **When** the customer presses dislike, **Then** a text field appears and the submitted comment is recorded with the rating.
3. **Given** recorded ratings, **When** the team reviews them, **Then** liked answers can be identified as training candidates and disliked ones can be read with their comments.

---

### Edge Cases

- The customer writes in a language other than Russian; the assistant replies in Russian and says that only Russian is supported.
- A product is out of stock or its price changes between selection and confirmation; the assistant shows current data and reconfirms before creating the order.
- The customer asks for a product attribute that is not in the catalog data; the assistant says it does not have that information instead of guessing.
- The shop's system is unavailable when searching, ordering or checking status; the assistant tells the customer and offers a manager handoff, and never claims an order was created if it was not.
- The customer repeats the confirmation or sends it twice; only one order is created.
- Phone number or address is malformed or incomplete; the assistant asks again.
- The customer tries to extract unrelated content or make the assistant act outside shop topics; the assistant stays on topic.
- The customer pastes payment card details; the assistant does not process them and warns the customer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept customer requests in free-form text and extract search parameters such as category, budget, purpose, brand and colour.
- **FR-002**: The system MUST search only the connected shop's catalog and MUST present only products that exist in it, with their real photo, price and availability.
- **FR-003**: The system MUST ask no more than two clarifying questions per search before presenting options.
- **FR-004**: When no product matches, the system MUST say so explicitly and offer the closest in-catalog alternatives.
- **FR-005**: The system MUST NOT state product facts (specifications, prices, stock) that are not supported by catalog data.
- **FR-006**: The system MUST collect name, phone, delivery address and delivery method before an order is placed.
- **FR-007**: The system MUST show an order summary (items, quantities, prices, total) and obtain explicit customer confirmation before creating the order.
- **FR-008**: The system MUST NOT create an order without explicit confirmation, and MUST create at most one order per confirmation.
- **FR-009**: After an order is created, the system MUST give the customer a payment link or QR code.
- **FR-010**: The system MUST NOT request, accept, store or repeat payment card or other payment credentials in any conversation.
- **FR-011**: The system MUST report the current status of a customer's order on request.
- **FR-012**: The system MUST proactively notify the customer when the status of their order changes.
- **FR-013**: The system MUST be reachable through at least one messenger (Telegram first) and through the shop's website chat.
- **FR-014**: The system MUST keep one shared conversation history per customer across channels and use it as context.
- **FR-015**: The system MUST let shop managers view all conversations, including full history, and join any conversation at any time.
- **FR-016**: The system MUST hand a conversation to a manager when the customer asks for a human or when the assistant is not confident in its answer.
- **FR-017**: While a manager is handling a conversation, the system MUST NOT send automated replies until the conversation is handed back.
- **FR-018**: The system MUST show like and dislike controls on every assistant answer, store the rating with the answer, and capture an optional text comment on dislike.
- **FR-019**: The system MUST allow liked answers to be identified as training candidates.
- **FR-020**: The system MUST read catalog data and create and read orders in the existing shop only through the shop's official programmatic interface, never by accessing its data store directly.
- **FR-021**: The system MUST conduct all customer conversations in Russian; other languages are not supported.
- **FR-022**: The assistant's behaviour MUST be covered by automated tests, including tests that verify it never offers non-existent products, never creates an order without confirmation and never requests payment details, accounting for the variability of language-model responses.

### Key Entities

- **Customer**: A person talking to the assistant; identified across channels; has contact details and a conversation history.
- **Conversation**: The shared history of messages between a customer, the assistant and possibly a manager; has a state (assistant-handled, awaiting manager, manager-handled).
- **Message**: A single customer, assistant or manager message with channel, time and, for assistant answers, an attached rating.
- **Rating**: A like or dislike on an assistant answer, with optional comment.
- **Product**: A catalog item owned by the shop: name, category, brand, attributes, photo, price, availability.
- **Search Request**: The parameters extracted from a customer's description.
- **Order**: A purchase created in the shop: customer details, items, quantities, prices, delivery method, address, payment link or QR code, status.
- **Manager**: A shop employee who supervises conversations and takes over handoffs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an automated evaluation set of at least 100 diverse product requests, 100% of the products the assistant recommends exist in the catalog.
- **SC-002**: At least 90% of evaluation requests with a satisfiable need return at least one product that meets every stated constraint (such as budget and category).
- **SC-003**: A customer can go from a first message to a confirmed order with a payment link in under 5 minutes of conversation.
- **SC-004**: In 100% of tested conversations, no order exists before explicit confirmation, and no conversation contains a request for payment credentials.
- **SC-005**: Customers receive an order status answer within 10 seconds of asking, and status-change notifications within 1 minute of the change.
- **SC-006**: A conversation continued in a second channel retains its context in 100% of tested cases.
- **SC-007**: When a customer asks for a human, the conversation is visible to a manager as awaiting handoff within 10 seconds.
- **SC-008**: At least 80% of the pilot shop's customer questions about product selection are resolved without manager involvement, as measured over the pilot period.

## Assumptions

- The shop already exists and exposes a documented programmatic interface for catalog, order creation and order status; the assistant does not replace or modify the shop itself.
- The first release targets a test electronics shop used for demonstration, not a production business.
- The shop's payment system provides a payment link or QR code for a created order; the assistant does not process payments itself.
- Customers are identified across channels by a shared identifier they provide (for example their phone number) when they first order or ask to link channels.
- Managers use a simple web interface to view and join conversations; its detailed design is left to planning.
- Telegram is the first messenger; other messengers can be added later.
- The shop is Russian-language, so all customer-facing conversations are in Russian only; all project documentation and developer discussion are in English.
- The test electronics shop, its product catalog and its data-loading script are delivered by feature `001-test-shop`, which this feature depends on.
- Out of scope for this feature: voice messages, returns and refunds, analytics for managers, and telephony.
