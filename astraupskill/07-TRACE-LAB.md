# Trace the tuple across both operations

Before executing the harness, fill the empty columns of your own copy of this
table. “Downstream” excludes the initial event lookup and means conflict reads,
schedule reads, booking persistence and unit-of-work save. Counting the first
lookup as a failure would obscure the fact that persisted event attributes are
needed to decide whether the tuple is valid. The guard needs evidence; it does
not need a booking write to obtain that evidence.

| Persisted event | Booking result | Availability result | Allowed downstream work |
|---|---|---|---|
| Missing | EventType.NotFound | EventType.NotFound | None |
| Tenant T, owner J, active | EventType.NotFound | EventType.NotFound | None |
| Tenant U, owner H, active | EventType.NotFound | EventType.NotFound | None |
| Tenant T, owner H, inactive | EventType.Inactive | EventType.Inactive | None |
| Tenant T, owner H, active | Continue existing rules | Continue existing rules | Operation-specific |

For a successful booking, continue the last row: compute end time, query conflicts,
construct Invitee, create Booking, Add, SaveChanges and return the response. Mark
which values come from the command and which come from the event aggregate.
The host is caller supplied but checked against stored ownership. Duration comes
from the selected event; the client cannot independently choose an end time in
this command. Tenant comes from the resolved request context.

For availability, continue with GetDefaultByUserAsync. A null schedule returns
an empty list without reading bookings. A real schedule would lead into timezone,
date and overlap logic, which the new guard does not redesign. The harness's
positive query case intentionally stops at the null-schedule branch; do not
extend its success claim to daylight-saving conversion or slot generation.

Finally, overlay the original source on the wrong-host row. The old code could
continue with E's duration and H's calendar. Explain that mismatch to a peer
without using the word “validation.” If your explanation names both resources,
their owner and the prohibited interaction, you have identified the underlying
CRUD invariant rather than memorized a conditional expression.
