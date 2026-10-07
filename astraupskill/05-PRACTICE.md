# Practice before reading the answers

Use tenant T, requested host H and event E for every prediction. Write the
event's stored TenantId, OwnerId and IsActive beside the command. Then record
the expected result, conflict-query count, schedule-query count, Add count and
SaveChanges count. Answer from the handlers rather than an imagined framework
policy, and label any answer that requires an integration test.

The harness command used in every **Check** below is
`dotnet run --project tests/NetCalendy.ScopeHarness --configuration Release`
(.NET 10 SDK; see 04 for why the xUnit Application suite is not an option on a
clean clone). Each harness case prints `ok - <name>` or `not ok - <name>`.

## Prediction exercises

**P1. The legal tuple.** E belongs to T and H and is active; the conflict
query returns no rows.
- Goal: predict the persisted Booking identities and a thirty-minute event's end.
- **Check:** harness case `matching active event creates and saves the
  correctly scoped booking` asserts one Add, one save, host/tenant/event IDs
  and `EndTime == StartTime + 30` (`Program.cs:41-50`).

**P2. Wrong host.** Change only E.OwnerId to J.
- Goal: predict booking and availability behavior, and explain why checking
  that H exists would not repair the relationship.
- **Check:** cases `booking rejects foreign-host before conflict lookup or
  writes` and `availability rejects foreign-host before schedule or booking
  reads`. Then temporarily delete `|| eventType.OwnerId != hostId` from
  `CreateBookingHandler.cs:27` only, rerun, and confirm exactly the booking
  foreign-host case reports `not ok`. Revert with `git checkout -- src`.

**P3. Wrong tenant.** Restore OwnerId to H but change E.TenantId to U.
- Goal: predict the error and the first downstream call that must not occur
  in each handler.
- **Check:** the two `foreign-tenant` cases; the assertion text
  `rejected request reached downstream state` / `rejected request read
  schedules or bookings` names the forbidden interaction if your guard is late.

**P4. Inactive, then inactive plus wrong host.**
- Goal: predict both paths for an inactive matching event, then decide which
  error wins when inactivity and a wrong host combine.
- **Check:** the `inactive` cases expect `EventType.Inactive`. For the
  combination, read the order of `CreateBookingHandler.cs:27` and `:29`; no
  harness case combines them, so add one (scenario `foreign-host` plus
  `Event.Deactivate()`) and assert `EventType.NotFound`.

**P5. No schedule.** Keep the event valid but return no schedule.
- Goal: explain why this differs from a missing event and whether
  availability should be an error.
- **Check:** case `matching event without a schedule still returns an empty
  slot list` asserts success, zero slots and exactly one downstream read.

## Implementation exercises

**I1. Cancellation-token propagation.**
- Goal: extend the harness so the event repository records the token passed
  to `GetByIdAsync`, and decide whether the assertion protects behavior or a
  detail.
- **Check:** pass a `CancellationTokenSource().Token` into `Handle`; the new
  case passes, and replacing `ct` with `default` at `CreateBookingHandler.cs:24`
  makes it fail.

**I2. The missing-claim request.**
- Goal: add a harness case whose `ICurrentTenantService` double returns
  `null` for `TenantId`, and predict the outcome before running it.
- **Check:** the case observes an `InvalidOperationException` from
  `CreateBookingHandler.cs:21` (`TenantId!.Value`) before any repository call.
  Write down what HTTP status `ExceptionHandlingMiddleware.cs:31-38` turns that
  into, then compare with 06, finding A.

**I3. Controller mapping.**
- Goal: write an integration test that sends a mismatched public request and
  verifies response mapping without logging invitee personal data. State what
  infrastructure it needs.
- **Check:** in `tests/NetCalendy.Integration.Tests` (Docker required), a new
  `[Fact]` asserting `404` for a foreign-host event, with the bearer token set
  as in `CreateBookingEndpointTests.cs:26-27`. If you omit the token, record
  what you get instead.

## Senior design exercise

**S1. Simultaneous bookings.**
- Goal: propose a database-level protection against two simultaneous bookings
  of the same host and interval. Name the invariant, a constraint or locking
  strategy, transaction boundaries and the retry/error contract. Explain why
  the host/event guard neither solves nor prevents that race.
- **Check:** your proposal names the exact error code the loser receives and
  the HTTP status `ResultExtensions.cs:25-31` would map it to today. If you
  assumed 409, re-read 06, finding B. The full race test is
  [course exercise 3.1](course/EXERCISES.md).
