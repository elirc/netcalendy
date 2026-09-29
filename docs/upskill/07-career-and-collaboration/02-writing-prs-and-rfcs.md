# 02 — Writing PRs and RFCs

## Why Writing Matters

A PR description is the most-read documentation you produce. It's read by:
- Reviewers who need to understand what you did and why
- Future engineers debugging why a decision was made
- You, six months later, wondering what you were thinking

An RFC (Request for Comments) is a design document written before the code. It invites disagreement early — when changing direction is cheap — instead of after implementation.

---

## PR Description Template

Use this structure for any PR that changes behavior (not just formatting/typo fixes):

```markdown
## What

One sentence: what does this PR do?

## Why

Why is this change needed? What problem does it solve? Link to the ticket or issue.

## How

How did you approach it? Highlight non-obvious decisions. If you tried something else first, say so.

## Testing

How did you verify it works? What tests were added? What did you test manually?

## Risks / Followups

What could go wrong? What's not included in this PR that should be tracked?
```

---

## Good vs. Bad PR Descriptions (Using This Repo as Context)

### Bad PR description for TICKET-001:

> Add UpdateEventTypeValidator

That's it. No context, no why, no testing notes.

**Problems:** A reviewer doesn't know if they should block on missing edge cases. In 6 months, nobody knows why this validator was added.

---

### Good PR description for TICKET-001:

> **What:** Add `UpdateEventTypeValidator` — FluentValidation for `UpdateEventTypeCommand`
>
> **Why:** `PUT /api/event-types/{id}` accepted any input, including empty names and out-of-range durations. Without a validator, these reached the domain and failed with a 500 instead of a 400. Closes TICKET-001.
>
> **How:** Follows the same pattern as `CreateBookingValidator`. Registered automatically by MediatR's assembly scan via `DependencyInjection.cs`.
>
> Notable: Duration max is 480 minutes (8 hours). If a customer legitimately needs longer event types, this is a config change, not a code change.
>
> **Testing:** `dotnet test` passes. Manual: `PUT /api/event-types/{id}` with empty name returns 400 with `ValidationProblemDetails`.
>
> **Risks:** TICKET-M09 (integration test for the full validation pipeline) is a follow-up.

---

## RFC Template

Use an RFC when:
- A change touches more than 2 layers
- The approach has non-obvious tradeoffs
- Multiple team members would make different choices

```markdown
# RFC: [Title]

**Author:** [you]  
**Date:** [date]  
**Status:** Draft | In Review | Accepted | Superseded

## Problem

What problem are we solving? Why now?

## Goals

What should be true after this is done?

## Non-Goals

What are we explicitly not solving?

## Proposed Approach

Describe the approach. Include diagrams, code sketches, or data model changes.

## Alternatives Considered

What else did you consider? Why did you reject it?

## Tradeoffs

What does the proposed approach give up? What risks does it introduce?

## Open Questions

What do you need input on?
```

---

## Sample RFC: Transactional Outbox (PROJECT-S01)

```markdown
# RFC: Transactional Outbox for Domain Events

**Author:** [your name]  
**Date:** 2026-06-08  
**Status:** Draft

## Problem

Domain events (`BookingCreatedEvent`) are dispatched via MediatR after `SaveChangesAsync` in `AppDbContext`. If the domain event handler fails or the process crashes between the DB write and the `Publish` call, the side effect (email, audit log, webhook) is lost with no retry. This is "at-most-once" delivery — not acceptable for production.

## Goals

- Domain event side effects are delivered at least once, even across process restarts
- No coupling between booking creation and email delivery latency
- Observable: delivery attempts are logged and queryable

## Non-Goals

- Exactly-once delivery (idempotency is the handler's responsibility)
- Real-time delivery (within-second latency not required initially)

## Proposed Approach

Write domain events to an `outbox_messages` table in the same DB transaction as the entity save. A background service (`OutboxProcessorService : IHostedService`) polls the table every 5 seconds and publishes unprocessed messages.

**Schema:**
[...]

## Alternatives Considered

1. **Hangfire background jobs:** Simpler, but not transactional — job could be queued but not committed if the transaction rolls back.

2. **Keep current approach:** Acceptable for MVP. Not acceptable once we have any side effects that matter.

## Tradeoffs

- Adds DB polling (5s interval, 1 query per tick) — minor overhead
- Requires handlers to be idempotent (retry-safe)
- Schema migration required

## Open Questions

- Should processed messages be hard-deleted or soft-deleted (for debuggability)?
- What is the max retry count before a message is marked "dead"?
```

---

## Commit Message Discipline

One commit message convention matters more than which convention you use: **explain the why, not the what.**

**Bad:** `fix bug in handler`

**Bad:** `add tenant check to GetBookingHandler`

**Good:** `Return 404 for cross-tenant booking access to prevent IDOR`

The diff already shows what changed. The message should answer: why was this the right change to make?
