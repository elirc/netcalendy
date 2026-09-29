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

Do not infer PostgreSQL behavior from these doubles. Follow-up integration tests
should exercise tenant resolution, EF tracking and query filters, controller
mapping, MediatR validation and two simultaneous bookings. The application still
targets .NET 9; this isolated .NET 10 compilation verifies a selected source
slice and does not certify the application's original build configuration.
