# Exercises: from reading this system to proving it

Ordered by tier; each has a **done when**. Work on a branch, and write your
prediction down before every exercise — the learning is in grading it.
Revert Tier-1 breaks with `git checkout -- <file>` and never commit one.

## Baseline — know what actually runs here

```powershell
dotnet test tests/NetCalendy.Domain.Tests
dotnet run --project tests/NetCalendy.ScopeHarness --configuration Release
dotnet test tests/NetCalendy.Application.Tests   # read the note below first
```

Captured on this machine (Windows 11, .NET SDK 10.0.302, no .NET 9 runtime
installed — the first command needed `DOTNET_ROLL_FORWARD=LatestMajor` to run
the net9.0 test host):

- Domain.Tests: `Failed: 0, Passed: 14, Skipped: 0, Total: 14`
- ScopeHarness: `# tests 11 / # pass 11 / # fail 0`
- **Application.Tests does not compile** on a clean clone:

  ```
  src\NetCalendy.Application\DependencyInjection.cs(21,18): error CS1061:
  'IServiceCollection' does not contain a definition for 'AddValidatorsFromAssembly'
  ```

  The project references `FluentValidation` 11.11.0 but not
  `FluentValidation.DependencyInjectionExtensions`, where that extension
  method lives. This also means `dotnet test NetCalendy.sln` is red as a
  whole. Fixing it is exercise 1.4 — do it first if you want the handler
  suite available for the other exercises.
- Integration.Tests need Docker (Testcontainers + real Postgres) and were not
  run in this environment; nothing below quotes a result from them.

## Tier 1 — break and observe (half a day each)

**1.1 Delete the enforcement point.** In
`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`,
remove the three-line conflict check (lines 33-35: the
`GetConflictingBookingsAsync` call and the `Booking.TimeConflict` return).
*Predict first:* which of the three baseline commands notices? All of them?
*Done when:* you can name the one failing case without rerunning, and explain
why Domain.Tests stayed green (14/14 — the overlap math is intact; nobody
calls it). A completed run with real output is in
[worked-examples/remove-the-conflict-check.md](worked-examples/remove-the-conflict-check.md) —
do yours first.

**1.2 Break the timezone conversion.** In
`src/NetCalendy.Domain/Schedules/TimeSlot.cs`, change `ToUtc` to
`new DateTimeOffset(local, TimeSpan.Zero)` — i.e., "the schedule is probably
UTC anyway."
*Predict first:* which suite fails? (Spoiler shape, not spoiler answer: check
which tests ever call `ToUtc` with a non-UTC zone.)
*Done when:* you have run all three baseline commands with the break in place
and written one sentence stating honestly whether anything failed. If nothing
did, that *is* the finding: the conversion point is unpinned, and writing the
missing test — a schedule in `America/New_York`, a slot at 09:00 local,
asserting 13:00/14:00 UTC depending on the date — becomes your Tier-2 entry
ticket. Add a second test for the spring-forward 500 described in
[CODE-TOUR.md](CODE-TOUR.md) §6 and watch it fail against current code.

**1.3 Break the overlap math.** In
`src/NetCalendy.Domain/Bookings/Booking.cs:72-73`, change
`StartTime < end && EndTime > start` to `StartTime < end || EndTime > start`.
*Predict first:* which of the three `ConflictsWith` tests in
`tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs` fail,
and does the ScopeHarness notice too?
Then try the subtler edit instead: `EndTime >= start` (back-to-back meetings
now "conflict"). *Predict first:* does any test fail? The disjoint-range test
uses a one-minute gap, so look carefully before answering.
*Done when:* you can state which boundary (`end == start`) is unpinned and
have written the adjacency test that pins it — a booking ending 10:00 must
not conflict with one starting 10:00.

**1.4 Fix the baseline.** Add the
`FluentValidation.DependencyInjectionExtensions` package reference to
`src/NetCalendy.Application/NetCalendy.Application.csproj` (same 11.x line).
*Done when:* `dotnet test tests/NetCalendy.Application.Tests` builds and
runs, you have recorded its real pass count next to the baseline above, and
you can say why Domain.Tests and the ScopeHarness never tripped over this
(neither compiles `DependencyInjection.cs`).

## Tier 2 — extend inside the boundaries (1-3 days each)

Write the failing test first, in the style of the existing suites.

**2.1 Buffer times.** Add `BufferBeforeMinutes`/`BufferAfterMinutes` to
`EventType` (default 0, cap 120): a 30-minute meeting with a 10-minute buffer
blocks 40+ minutes of the host's calendar for conflict purposes, while the
invitee still sees a 30-minute meeting.
*Covers:* where derived time ranges belong (domain, not handler), and the
temptation to compute the buffer in two places that then drift.
*Done when:* domain tests pin that a booking 5 minutes after a buffered
booking conflicts; `GetAvailableSlots` stops offering slots inside the
buffer; the booking and availability paths share one piece of code that
answers "what interval does this booking occupy?".

**2.2 Cancellation window.** Add `MinCancellationNoticeMinutes` to
`EventType`; `CancelBookingHandler` refuses with a specific error
(`Booking.TooLateToCancel`) inside the window.
*Covers:* policy enforcement at a token-authenticated public endpoint, and
the `IDateTimeProvider` gap — you cannot test "55 minutes before start"
deterministically while the handler calls `DateTimeOffset.UtcNow` directly,
so this exercise forces that injection through.
*Done when:* handler tests cover just-inside and just-outside the window
using a fake clock, and no new `UtcNow` call was added to Domain or
Application code.

**2.3 .ics export with a real TZID.** Add
`GET /api/bookings/{id}/calendar.ics` returning an RFC 5545 event.
The trap to engineer around: emitting `DTSTART:20261108T013000Z` (UTC) is
easy and survives DST; emitting the invitee's local wall time requires a
`VTIMEZONE`/`TZID` block, and the stored `invitee_timezone` is an IANA name
while `TimeZoneInfo` on Windows historically used Windows IDs.
*Done when:* a test asserts byte-level on `DTSTART`/`TZID` lines for a
booking that crosses a DST boundary in `America/New_York`, and the file
imports cleanly into at least one real calendar client (document which one —
that part is manual evidence, labeled as such).

## Tier 3 — move a boundary (1-2 weeks each)

One page of design first: two alternatives, chosen tradeoffs, what evidence
would change your mind.

**3.1 The two-process booking race test — then make it pass.** Stockade's
method, this domain's stakes: start **two independent API processes** against
one Postgres (Testcontainers), seed one host with one free slot, fire N
simultaneous `POST /api/bookings` for that slot across both processes, assert
exactly one 201, N-1 409s, and exactly one non-cancelled row in `bookings`.
Two prerequisites the current code imposes: send a bearer token with a
`tenant_id` claim (`JwtTokenFactory`), because token-less requests 500 (see
[06 finding A](../06-SOLUTIONS-AND-REVIEW.md)); and expect today's losers to
get **400**, not 409, until you also fix the `ResultExtensions.cs:28` mapping
(06 finding B) — make that the first commit of this exercise.
*Predict first, in writing:* against current code, how often does it
oversell? (It will not fail every run — and a sometimes-failing test is the
scarier result; say why.)
Then fix it so the test passes deterministically. Acceptable designs to weigh:
a Postgres exclusion constraint on `(host_id, tstzrange(start_time, end_time))
WHERE status <> cancelled` with the 23P01 error translated to the same 409;
serializable isolation with retry; or an advisory lock per host. The gitjira
rule applies: the database must be the final authority, and the application
check's remaining job is the *good error*.
*Done when:* the race test passes 10 consecutive runs, reverting the fix
makes it fail at least sometimes (record the observed rate honestly), and the
409 body is still `Booking.TimeConflict`, not a raw constraint violation.

**3.2 Recurring availability with overrides.** Replace the single
day-of-week rule list with date-specific overrides ("off this Friday",
"extra hours Nov 30") and a slot generator that handles the spring-forward
gap and fall-back ambiguity explicitly instead of throwing.
*Done when:* property-style tests generate random rule sets across the two
US DST transition dates and assert no exception, no slot before "now", no
overlapping offered slots, and documented behavior (not accident) for 01:30
on fall-back night.

**3.3 Calendar sync as an external effect.** Push confirmed bookings to an
external calendar (fake provider over HTTP, relay-rebuild style). The current
after-save event publish (`AppDbContext.cs:44-47`) loses events on a crash —
so this is a transactional-outbox exercise: an `outbox` table written in the
same `SaveChangesAsync`, a separate drainer process, at-least-once delivery
with a dedupe key.
*Done when:* a test kills the process between commit and publish and the
booking still reaches the fake provider exactly once after restart; a
duplicate drain delivers nothing twice. Relay's provider lesson is the
grading rubric: the effect is the thing to prove, not the enqueue.

## Calibration

You are at the level this course aims at when you can state, unprompted:
which layer currently owns "no double booking" (one application check), what
it would take for the database to own it instead, how time enters this system
exactly once and where that conversion can throw, and the strength of each
claim you make — "14 domain tests pass" versus "no test proves two
simultaneous writers lose."
