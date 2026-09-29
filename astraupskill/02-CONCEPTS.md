# Referential consistency and public resource scope

A foreign key can prove that a referenced row exists. It does not necessarily
prove that two referenced rows belong together. A booking may contain a valid
event ID and a valid host ID while the event belongs to someone else. Treat
the request as a relationship tuple: resolved tenant, requested host, selected
event. The event's stored TenantId and OwnerId are the evidence used to validate
that tuple. Caller-provided IDs are assertions to check, not authority.

This is different from authenticating the invitee. The application permits
public booking, so an anonymous visitor can legitimately request a host's
published event type. The handler must preserve that product behavior while
refusing to combine another host with the selected event. Returning the same
not-found result for missing and mismatched resources avoids introducing a
separate response that confirms a resource outside the requested scope.

State is another dimension. An event type can belong to the correct host and
tenant but be inactive. Booking already rejected that case. Availability must
also reject it rather than advertise slots that cannot be booked through the
corresponding create path. Relationship checks come first, so inactive state
is not disclosed for a mismatched resource. Tests should name these as separate
conditions rather than merging every invalid input into a generic failure.

The comparisons use the domain's TenantId and UserId value objects, whose
record equality compares their values. Do not compare display names or slugs
when the aggregate already carries stable identities. Do not trust the client
to disable an invalid combination; direct callers can submit the same command.

Finally, scope validation is not concurrency control. A conflict query followed
by a write can still race with another booking. The new checks do not prove
availability membership, timezone correctness, atomic reservation or delivery
of domain events. Read the [domain booking rules](../src/NetCalendy.Domain/Bookings/Booking.cs)
and separate those concerns when designing the next experiment. One precise
invariant is easier to verify than an unsupported promise of complete safety.
