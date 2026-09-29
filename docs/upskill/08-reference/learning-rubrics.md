# Learning Rubrics

Observable behaviors at each level, plus self-assessment checklists tied to this codebase.

---

## Junior Engineer Rubric

A junior engineer working in this codebase can:

**Understanding**
- [ ] Trace a `POST /api/bookings` request from controller to DB write (using `05-key-flows.md`)
- [ ] Explain what `Result<T>` is and why it's used instead of exceptions
- [ ] Find where a FluentValidation rule is defined for a given command
- [ ] Read an EF migration and understand what SQL change it produces
- [ ] Explain what `IClassFixture<IntegrationTestWebAppFactory>` does in a test class

**Contributing**
- [ ] Add a validator for a new command, following the existing pattern
- [ ] Add a field to an existing response DTO and verify it appears in the response
- [ ] Write a domain unit test for an aggregate invariant (no mocks, no containers)
- [ ] Fix a bug reported by an integration test

**Communication**
- [ ] Write a PR description that explains what the change does and why
- [ ] Ask a specific question that includes what they've already tried

**Signs they're ready to level up:**  
They start spotting patterns before being told ("this new handler should also check tenant scope, right?"). They ask "why" before asking "how."

---

## Mid-Level Engineer Rubric

A mid-level engineer working in this codebase can:

**Understanding**
- [ ] Explain the dependency rule in Clean Architecture and identify where a violation would occur
- [ ] Explain why `AppDbContext` must use `IHasDomainEvents` rather than `AggregateRoot<object>`
- [ ] Trace a domain event from raise to dispatch, and identify what happens if the handler throws
- [ ] Explain RISK-001 (IDOR) and propose a fix without looking it up
- [ ] Explain the difference between `staleTime` and `gcTime` in React Query

**Contributing**
- [ ] Implement a new feature that spans Domain + Application + Infrastructure + API layers
- [ ] Design and write integration tests that verify both HTTP responses and DB state
- [ ] Add pagination to a list endpoint without breaking existing callers
- [ ] Identify and fix a missing tenant scope check in a handler

**Design**
- [ ] Recognize when a new repository method vs. a new handler query is the right choice
- [ ] Propose the right EF Core mapping for a new value object without being told
- [ ] Write an RFC for a multi-layer change before implementing it

**Signs they're ready to level up:**  
They ask "what could go wrong" before shipping. They review PRs looking at security and correctness, not just style.

---

## Senior Engineer Rubric

A senior engineer working in this codebase can:

**Systems thinking**
- [ ] Identify the full risk surface of a new feature before implementing it
- [ ] Design the outbox pattern and explain its reliability guarantees
- [ ] Explain under what load conditions the `GetAvailableSlots` handler becomes a bottleneck
- [ ] Design a migration strategy for adding a NOT NULL column to a 50M-row table
- [ ] Explain the failure modes when `publisher.Publish` is called after `SaveChangesAsync`

**Architecture**
- [ ] Decide when Event Sourcing would help vs. hurt this system
- [ ] Design a webhook delivery system with retry and idempotency guarantees
- [ ] Own the risk register and triage which risks to fix in which order

**Mentorship**
- [ ] Explain CQRS to a junior in terms of this specific codebase (not abstract theory)
- [ ] Write a code review comment that teaches a pattern, not just corrects the mistake
- [ ] Write a ticket (like the ones in `01-good-first-tickets.md`) that a junior can complete without being blocked

**Ownership**
- [ ] Identify what's missing in this codebase's observability and propose a fix
- [ ] Own a deploy and rollback plan for a migration
- [ ] Set up rate limiting and explain the threat model it addresses

---

## Self-Assessment Checklist

Rate yourself on each item: ✅ Confident | 🟡 Partial | ❌ Not yet

### Domain / Architecture
- [ ] I can explain the dependency rule and catch violations in PR review
- [ ] I understand why `Booking.Create` is a factory method, not a constructor
- [ ] I can explain what `IHasDomainEvents` is and why it exists
- [ ] I understand how EF Core owned types differ from a related entity
- [ ] I can explain Railway-Oriented Programming in one sentence

### Application / Testing
- [ ] I can write a handler test with mocked repositories (NSubstitute)
- [ ] I can write an integration test that verifies both HTTP and DB state
- [ ] I understand the difference between `IClassFixture` and per-test setup
- [ ] I can add a FluentValidation rule and test it

### Security / Reliability
- [ ] I check tenant scope when reviewing new GET endpoints
- [ ] I can explain RISK-001 (IDOR) and the fix
- [ ] I understand why domain events fire after `SaveChangesAsync`
- [ ] I know what the outbox pattern is for

### Collaboration
- [ ] I write PR descriptions that explain why, not just what
- [ ] I label review comments as blocking vs. non-blocking
- [ ] I ask questions that include what I've already tried
