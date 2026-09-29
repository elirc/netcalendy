# Map the two public paths

[BookingsController](../src/NetCalendy.Api/Controllers/BookingsController.cs)
accepts CreateBookingCommand on POST api/bookings with AllowAnonymous. It sends
the command through the mediator and converts the handler result into an HTTP
response. The command contains EventTypeId, HostUserId, StartTime and invitee
details. Tenant identity comes from ICurrentTenantService rather than a tenant
field copied from the command. These are distinct sources of input, so write
them in separate columns when tracing a request.

[CreateBookingHandler](../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)
loads the event type, checks scope and active state, calculates the end from
its duration, queries conflicting bookings, validates the invitee, constructs
Booking, adds it and saves the unit of work. EventType carries TenantId and
OwnerId; Booking carries TenantId, HostId and EventTypeId. The new comparison
connects those identities before the handler constructs a new aggregate.

[SchedulesController](../src/NetCalendy.Api/Controllers/SchedulesController.cs)
exposes GET api/schedules/available-slots anonymously. Its existing date-range
checks reject reversed ranges and ranges over sixty days. The query handler
loads the event type before the default host schedule and existing bookings.
It now applies the same scope and active-state policy before those reads.
A matching event with no schedule still returns an empty successful list.

The infrastructure repository inherits GetByIdAsync from RepositoryBase,
which uses EF FindAsync. The handler should not assume that a generic lookup
proves the relationship between independently supplied IDs. Repository query
filters may provide additional protection, but the source-level guard states
the operation's own contract and remains observable in a focused test.

For navigation practice, follow one valid command to bookings.Add and then
repeat with the wrong host. Circle the earliest branch that differs. For the
query path, circle GetDefaultByUserAsync: a wrong host must never reach it.
This map makes read avoidance as reviewable as write avoidance and clarifies
why both public entry points belong in this bounded change.
