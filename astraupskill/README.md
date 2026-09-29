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

Study the chapters in order, keeping the source open. First draw the route,
handler, aggregate and repository path. Then predict which downstream calls
are allowed for each input tuple. Compare the original snapshots with the
guard, run the focused harness, and attempt the practice before opening its
separate answers. The trace table provides a review aid rather than a claim
that every component of the web application was exercised.

The [harness](../tests/NetCalendy.ScopeHarness/Program.cs) compiles production
source into a dependency-free .NET 10 executable. The application still targets
.NET 9. Repository and mediator contracts in the harness are explicit test
boundaries; ASP.NET middleware, MediatR pipelines, EF Core and PostgreSQL are
not executed. Read VERIFICATION for the captured result. A senior conclusion
should identify both the relationship now enforced and the scheduling,
concurrency and deployment questions this small change does not answer.
