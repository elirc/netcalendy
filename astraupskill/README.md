# NetCalendy: validate the relationship, not just the identifier

A booking command contains an event-type ID and a host-user ID. Each can be
well formed while their combination is wrong. This lab follows the actual
[booking handler](../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)
and [availability handler](../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)
to repair that boundary. Before the change, finding the event type was enough
to continue using a caller-supplied host. The corrected handlers require the
event type to belong to both the resolved tenant and requested host.

This is especially useful CRUD practice because the endpoints intentionally
allow anonymous invitees. Requiring a signed-in user would change the booking
product. Instead, the operation must establish which public resource the
request describes. Missing resources and mismatched resource relationships
produce the same EventType.NotFound result. An inactive matching event type
produces EventType.Inactive, and availability now agrees with booking on that
state rule. Legal booking and missing-schedule behavior remain visible in tests.

## Reading order

1. [01-CODEBASE-MAP](01-CODEBASE-MAP.md) — the two public paths, with line numbers.
2. [02-CONCEPTS](02-CONCEPTS.md) and [03-WORKED-CHANGE](03-WORKED-CHANGE.md) — the
   tuple invariant and the diff (compare against `snapshots/`, which hold the
   *pre-change* handlers).
3. [04-TESTING-AND-DEBUGGING](04-TESTING-AND-DEBUGGING.md) — which suites exist and
   which actually run.
4. [05-PRACTICE](05-PRACTICE.md), then [06-SOLUTIONS-AND-REVIEW](06-SOLUTIONS-AND-REVIEW.md)
   (06 also holds verified review findings the lab did not fix).
5. [07-TRACE-LAB](07-TRACE-LAB.md) and [VERIFICATION](VERIFICATION.md).
6. Then the follow-on [course/](course/LEARNING-PATH.md) on time and concurrency.

Read 06's findings before trusting the word "anonymous" above: the endpoints
are `[AllowAnonymous]`, but the tenant is read only from a JWT claim, so a
request without a token never reaches the guard this lab adds.

The [harness](../tests/NetCalendy.ScopeHarness/Program.cs) compiles production
source into a dependency-free .NET 10 executable. The application still targets
.NET 9. Repository and mediator contracts in the harness are explicit test
boundaries; ASP.NET middleware, MediatR pipelines, EF Core and PostgreSQL are
not executed. Read VERIFICATION for the captured result. A senior conclusion
should identify both the relationship now enforced and the scheduling,
concurrency and deployment questions this small change does not answer.
