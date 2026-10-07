# Map the two public paths

[BookingsController](../src/NetCalendy.Api/Controllers/BookingsController.cs)
accepts CreateBookingCommand on POST api/bookings with `[AllowAnonymous]`
(lines 19-28). It sends the command through the mediator and converts the
handler result with `ToCreatedResult`. The command contains EventTypeId,
HostUserId, StartTime and invitee details. Tenant identity comes from
ICurrentTenantService rather than a tenant field copied from the command.
These are distinct sources of input, so write them in separate columns when
tracing a request.

Where does the tenant actually come from? Follow it, because it is not where
the middleware suggests:

| Step | File:line | What it does |
|---|---|---|
| 1 | `src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs:12-21` | Resolves `X-Tenant-Slug` header or subdomain and writes `context.Items["TenantId"]` |
| 2 | `src/NetCalendy.Infrastructure/Services/CurrentTenantService.cs:8-15` | Reads the **`tenant_id` JWT claim** only; nothing in `src/` reads `Items["TenantId"]` |
| 3 | `CreateBookingHandler.cs:21` / `GetAvailableSlotsHandler.cs:20` | `TenantId.From(currentTenant.TenantId!.Value)` — throws if no claim |

So "anonymous" in practice means "any caller holding a JWT with a tenant
claim". 06-SOLUTIONS-AND-REVIEW, finding A, works through the consequence.

[CreateBookingHandler](../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)
loads the event type (line 24), checks scope (line 27) and active state
(line 29), calculates the end from its duration (31), queries conflicting
bookings (33-35), validates the invitee (37-38), constructs Booking (40-41),
adds it and saves the unit of work (43-44). EventType carries TenantId and
OwnerId; Booking carries TenantId, HostId and EventTypeId. The line-27
comparison connects those identities before the handler constructs a new
aggregate.

[SchedulesController](../src/NetCalendy.Api/Controllers/SchedulesController.cs)
exposes GET api/schedules/available-slots anonymously (lines 26-42). Its
date-range checks (36-38) reject reversed ranges and ranges over sixty days
before the mediator is called. The [query handler](../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)
loads the event type (line 23), applies the same scope guard (25-26) and
active-state guard (27-28), then reads the default host schedule (30) and
existing bookings (40). A matching event with no schedule returns an empty
successful list (line 31).

The infrastructure repository inherits GetByIdAsync from
[RepositoryBase](../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs)
(lines 15-16), which uses EF `FindAsync` by primary key only. There is **no**
EF query filter on EventType: the only `HasQueryFilter` in the codebase is
`UserConfiguration.cs:40` (`u => u.IsActive`). The source-level guard is
therefore the only tenant/owner check on this path, not a second layer.

For navigation practice, follow one valid command to `bookings.Add` and then
repeat with the wrong host. Circle the earliest branch that differs (line 27).
For the query path, circle `GetDefaultByUserAsync` (line 30): a wrong host must
never reach it. This map makes read avoidance as reviewable as write avoidance
and clarifies why both public entry points belong in this bounded change.
