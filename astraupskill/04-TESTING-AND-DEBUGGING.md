# Observe the first forbidden interaction

Run `dotnet run --project tests/NetCalendy.ScopeHarness --configuration Release`
from this project root with .NET 10 installed. The harness project links the
actual domain source and both application handlers; it does not copy their
conditions into test functions. Its small MediatR interfaces support compilation
only. DispatchProxy implementations provide repository contracts, count calls
and fail if an unexpected method is invoked. The test result is handler evidence,
not a test of dependency injection or mediator dispatch.

For each public path, the fixtures cover a missing event, another host's event,
another tenant's event and an inactive matching event. Assert the exact error
code and zero downstream calls. If the error is correct but a schedule read
already occurred, the guard is too late. If no writes occur only because a
repository double throws, the test has not demonstrated deliberate rejection.
Both the result and the call trace are necessary to distinguish those failures.

The positive booking fixture records the actual Booking supplied to Add and
counts SaveChanges. Verify tenant, host, event ID and derived duration. Another
fixture returns a conflicting booking and verifies the existing conflict result
without Add or SaveChanges. The availability success fixture returns no default
schedule, confirming that an empty list remains a legitimate successful read.

When debugging, first compare the command's host with EventType.OwnerId, then
the current tenant with EventType.TenantId. Check value-object values rather
than assuming separately constructed objects have different identity. Next
inspect active state and the repository call log. Finally inspect result mapping
only if handler behavior is correct and the observed HTTP response differs.

## What else exists, and what runs

Counted from `[Fact]`/`[Theory]` attributes (no `[InlineData]` rows exist):

| Project | Tests | Runs without Docker? |
|---|---|---|
| `tests/NetCalendy.Domain.Tests` | 14 (BookingAggregateTests 8, ScheduleAggregateTests 6) | Yes |
| `tests/NetCalendy.Application.Tests` | 5 (CreateBookingHandlerTests 3, CreateEventTypeHandlerTests 2) | **Does not compile**: `DependencyInjection.cs:21` calls `AddValidatorsFromAssembly`, but `NetCalendy.Application.csproj` references only `FluentValidation`, not `FluentValidation.DependencyInjectionExtensions` |
| `tests/NetCalendy.Integration.Tests` | 7 (CreateBookingEndpointTests 3, EventTypesEndpointTests 4) | No — Testcontainers Postgres |
| `tests/NetCalendy.ScopeHarness` | 11 runtime cases (`Program.cs` `Run(...)` calls) | Yes (.NET 10 SDK) |

None of the existing xUnit tests covers the scope guard; the harness is its
only evidence. Note too that the harness's `ICurrentTenantService` double
always returns a tenant (`Program.cs:126`), so it cannot show what happens when
the JWT claim is absent — see 06, finding A.

Do not infer PostgreSQL behavior from these doubles. Follow-up integration tests
should exercise tenant resolution, EF tracking and query filters, controller
mapping, MediatR validation and two simultaneous bookings. The application still
targets .NET 9; this isolated .NET 10 compilation verifies a selected source
slice and does not certify the application's original build configuration.
