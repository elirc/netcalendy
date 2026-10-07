# Code tour: one promise, end to end

This tour follows a single promise through every layer:

> **A host is never booked twice for the same time.**

Read it with the files open. Line numbers refer to the source at the commit
this tour was written against; if they drift, search for the quoted code.
The ending matters more than the middle: the promise is enforced in exactly
one place, and that place is weaker than you will assume after reading the
README.

## 0. Set the scene

An invitee books anonymously: `POST /api/bookings` carries an event-type ID,
a host-user ID, a start time, and the invitee's details. No login — the
[README](../../README.md) API table marks it public. So whatever stops a
double booking cannot live in authentication; it must live in the data path.
(Caveat verified 2026-10-06: the handler still needs a `tenant_id` JWT claim to
resolve the tenant, so a truly token-less request fails with a 500 before
reaching any of the code below. See
[06-SOLUTIONS-AND-REVIEW](../06-SOLUTIONS-AND-REVIEW.md), finding A. The race
in this tour applies to any caller who does carry the claim.)

## 1. The client asks for slots, then posts one

- `web/src/api/bookings.ts:11` — `getAvailableSlots()` calls
  `GET /schedules/available-slots`. The server computes openings; the client
  only renders them.
- `web/src/api/bookings.ts:16` — `createBooking()` posts the chosen slot.
  Between these two calls is the race window this whole tour is about:
  nothing reserves a slot when it is displayed.

Predict before §4: when two clients post the same slot, what stops the second
write — the API, the handler, a transaction, or the database?

## 2. The API layer adds nothing to this promise

- `src/NetCalendy.Api/Controllers/BookingsController.cs:19-28` — the POST is
  `[AllowAnonymous]` and forwards straight to MediatR. It advertises a 409
  (`ProducesResponseType(typeof(ProblemDetails), 409)`) but produces none
  itself — and neither does the mapper: `ResultExtensions.cs:28` sends a code
  to 409 only if it contains `.Conflict`, and `Booking.TimeConflict` does not,
  so a refused double booking actually returns **400**.
- `src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs` — resolves
  `X-Tenant-Slug`/subdomain to a tenant ID and stores it in
  `HttpContext.Items`, which nothing reads (`CurrentTenantService` uses the JWT
  claim). Tenancy scoping, not concurrency.
- FluentValidation (`CreateBookingValidator.cs`) checks shape — email format,
  required fields — never other rows.

## 3. Availability: where time zones enter and leave

`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`:

- Lines 23-28 — the relationship guard (event type belongs to this tenant
  **and** this host). That is the previous lab's territory
  ([../README.md](../README.md)); this tour starts where it ended.
- Lines 30-35 — the host's `Schedule` carries a timezone *string*; the
  handler resolves it with `TimeZoneInfo.FindSystemTimeZoneById` and returns
  `Schedule.InvalidTimezone` if the OS does not know it.
- `src/NetCalendy.Domain/Schedules/Schedule.cs:53-66` — `GetAvailableSlots`
  slices each day's availability rule into duration-sized local wall-clock
  slots (`TimeOnly` arithmetic, no time zone yet).
- `src/NetCalendy.Domain/Schedules/TimeSlot.cs:5-9` — `ToUtc()` is the single
  conversion point: local `DateTimeKind.Unspecified` →
  `TimeZoneInfo.ConvertTimeToUtc`. From here on, everything is UTC
  (`DateTimeOffset` columns; `Booking.CreatedAt`, `StartTime`, `EndTime` all
  UTC). One conversion point is the right design. Its edges are in §6.
- Handler lines 50-53 — slots in the past are dropped, then slots that
  overlap an existing booking are filtered out via
  `Booking.ConflictsWith` (`src/NetCalendy.Domain/Bookings/Booking.cs:72-73`):
  `Status != Cancelled && StartTime < end && EndTime > start` — the standard
  half-open overlap test, cancelled bookings free their slot.

But this filter only shapes what is *offered*. An attacker — or just a slow
tab — can POST any start time it likes. Nothing in the booking path checks
that the posted time was ever an offered slot (it does not consult the
schedule at all — book a Sunday 3am and, conflicts aside, it saves). So the
promise cannot be enforced here either.

## 4. The enforcement point

`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs:33-35`:

```csharp
var conflicts = await bookings.GetConflictingBookingsAsync(hostId, cmd.StartTime, endTime, ct);
if (conflicts.Count > 0)
    return Result.Failure<CreateBookingResponse>(Error.Conflict("Booking.TimeConflict", ...));
```

backed by the repository query at
`src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs:16-23`
(same overlap predicate, in SQL, excluding cancelled), then `Add` +
`SaveChangesAsync` at handler lines 43-44.

**This is the entire mechanism: a check-then-insert in application code.**
Now look at what is *not* there:

- **No transaction** spans the check and the insert. `SaveChangesAsync`
  (`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs:31-50`) wraps
  only its own statements; the conflict SELECT ran earlier, outside any
  shared scope, at Postgres's default read-committed isolation.
- **No database constraint** restates the rule.
  `src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs:51-53`
  defines three indexes; `(HostId, StartTime)` is **not unique** (and
  uniqueness on start time would not even cover partial overlaps — Postgres
  would need an exclusion constraint on a time range, which EF model
  configuration here never creates). The only unique index in the bookings
  table is on `CancellationToken`.
- The migrations folder
  (`src/NetCalendy.Infrastructure/Persistence/Migrations/`) contains only a
  README — there is no committed schema to check against the model.

So: two requests for the same slot that interleave between line 33 and line
44 **both see zero conflicts and both insert**. The window is a few
milliseconds wide, which is exactly why no manual test has ever shown it.
Compare stockade, where the same shape of check was backed by a
`WHERE on_hand-reserved>=?` guard and a schema CHECK, and gitjira, where the
compare-and-swap UPDATE made the database the final authority. Here the last
line of defense is the first line of defense. The README's comparison table
("production-grade", "no mock/real divergence risk") is about test fidelity
and layering — it is accurate about those — but nothing in it should be read
as a concurrency guarantee. There isn't one.

## 5. The tests that pin the promise (and how far)

- `tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs:79-105` —
  pins `ConflictsWith` overlap math: overlapping true, disjoint false,
  cancelled false. Note what is unpinned: the back-to-back case
  (`end == next.start`) — the disjoint test uses a one-minute gap.
- `tests/NetCalendy.Application.Tests/Bookings/CreateBookingHandlerTests.cs:51-72` —
  pins that the handler refuses when the (mocked) repository reports a
  conflict. Sequential by construction; a mock cannot race.
- `tests/NetCalendy.ScopeHarness/Program.cs:55` — "matching active event
  retains existing conflict rejection": same guarantee, compiled from
  production source, runnable without Docker. The
  [worked example](worked-examples/remove-the-conflict-check.md) shows it
  catching a deleted guard.
- `tests/NetCalendy.Integration.Tests/BookingsEndpoints/CreateBookingEndpointTests.cs` —
  three cases through real HTTP and real Postgres (Testcontainers), but none
  posts the same slot twice — not even sequentially. The conflict path has
  never been exercised end to end, let alone concurrently — which is how its
  400-instead-of-409 mapping (§2) went unnoticed.

Honest strength statement: *the suites prove the handler refuses a conflict
it can see; no test in this repository proves two simultaneous requests
cannot both succeed.* Writing that test is exercise 3.1.

## 6. Where the promise stops

- **The race in §4** — the headline gap.
- **DST spring-forward:** `TimeSlot.ToUtc` converts a `DateTimeKind.Unspecified`
  local time; for a wall-clock time that does not exist (02:30 on the
  spring-forward night), `ConvertTimeToUtc` throws `ArgumentException`, which
  `GetAvailableSlotsHandler` does not catch — the whole availability request
  for that date range fails with a 500 via `ExceptionHandlingMiddleware`.
  (Asserted from the documented behavior of `ConvertTimeToUtc`; turning it
  into a failing test is exercise 1.2.)
- **DST fall-back:** ambiguous local times silently map to the standard-time
  offset, so one of the two real occurrences of 01:30 can never be offered —
  a capacity loss, not a corruption.
- **Display:** `GetAvailableSlotsHandler.cs:55` labels each slot with
  `startUtc.ToString("hh:mm tt")` — a **UTC** wall time shown to an invitee
  who supplied their own timezone. The invitee's timezone is validated,
  stored (`invitee_timezone`), and never used in any computation.
- **The clock itself:** `Booking.Create`
  (`src/NetCalendy.Domain/Bookings/Booking.cs:53`) and the handler's
  past-slot filter both call `DateTimeOffset.UtcNow` directly, although an
  `IDateTimeProvider` exists at
  `src/NetCalendy.Application/Common/IDateTimeProvider.cs` and is registered
  in Infrastructure. Boundary times cannot be tested deterministically, and
  "in the past" is defined by the API server's clock alone.
- **Cancellation + rebook:** `Cancel` and `Create` are separate requests with
  no ordering guarantee; a cancel racing a rebook of the same slot is governed
  by the same unprotected window as §4.
- **After the commit:** domain events are published *after* `SaveChangesAsync`
  returns (`AppDbContext.cs:44-47`) with no outbox — a crash between the two
  loses the `BookingCreatedEvent`. That is relay-rebuild's exactly-once
  problem surfacing here, and it is where Tier 3's calendar-sync exercise
  begins.

When you can say why the availability filter cannot replace the handler
check, why the handler check cannot survive two processes, and what the
database would have to assert so that it could — you understand this system,
and you are ready for [EXERCISES.md](EXERCISES.md).
