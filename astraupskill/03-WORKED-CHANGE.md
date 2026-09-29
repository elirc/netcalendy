# Worked change: stop a mismatched tuple early

Consider tenant T, host H and event E. E exists but OwnerId is J. Before the
change, the booking handler accepted E, used its duration, searched H's bookings
and constructed a booking connecting E to H. No check established that H owned
E. The availability handler similarly combined E's duration with H's schedule.
Compare the originals in snapshots with the current handlers before reading
the replacement condition below.

```csharp
// Before: existence was the only resource check.
if (eventType is null)
    return Result.Failure<CreateBookingResponse>(Error.NotFound("EventType", cmd.EventTypeId));

// After: the loaded aggregate must match the complete requested scope.
if (eventType is null || eventType.TenantId != tenantId || eventType.OwnerId != hostId)
    return Result.Failure<CreateBookingResponse>(Error.NotFound("EventType", cmd.EventTypeId));
```

The placement is before duration calculation, conflict lookup, invitee creation
and persistence. A repository spy can therefore assert more than a returned
error: no conflict query, Add or SaveChanges call is allowed for the wrong host.
For the query handler, the corresponding assertion is no schedule or booking
read. The same failure code is used when the event is absent or outside scope.

The availability change additionally checks IsActive after scope succeeds.
It returns EventType.Inactive using the same validation message already present
in booking. It does not modify how schedules generate slots, how bookings are
filtered by time or how the controller constrains date ranges. A valid event
without a default schedule continues to return an empty list.

In the [runtime harness](../tests/NetCalendy.ScopeHarness/Program.cs), each invalid
case loads an actual EventType aggregate or null through a repository double.
The real handler executes the guard. Legal create and existing conflict cases
then confirm that the repair did not turn every request into a failure. This
is the central review pattern: demonstrate the prohibited interaction and a
neighboring allowed interaction through the production operation itself.
