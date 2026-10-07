# Answers and review rubric

Case one succeeds when the remaining invitee and domain validations also pass.
The saved booking carries tenant T, host H and E's actual ID; its end is the
requested start plus E.Duration.Minutes. The harness verifies one Add and one
SaveChanges call. An answer that merely says “201” skips the handler's contract:
the HTTP status belongs to controller mapping and was not executed by this test.

Case two returns EventType.NotFound before conflict or schedule lookup. H's
independent existence would not prove ownership of E. Case three returns the
same result for a tenant mismatch. In both cases the event lookup is allowed,
but operations on the requested host's calendar must not follow. Case four
returns EventType.Inactive only for a matching tuple. A wrong-host inactive event
returns not found because scope is checked before state. Case five succeeds with
an empty slot list after one default-schedule lookup; an absent schedule is an
existing supported empty result, unlike an absent selected event.

The cancellation-token exercise is useful if cancellation propagation is part
of the operation's documented contract. Avoid asserting arbitrary local variable
names or object construction order. A controller integration test adds HTTP
binding, tenant resolution and error translation; it cannot be replaced by a
handler double returning a fabricated status code. Its evidence should remain
separate from the focused source-slice compilation.

Score a review on four dimensions: correct resource relationship, early rejection,
preserved legal behavior and accurate evidence boundaries. Require the author to
show both handlers because availability and booking form a public workflow.
Reject client-only checks and checks performed after Add. Ask which error is
returned for a resource outside scope and whether it reveals more information
than the existing not-found path.

For the concurrency design, there is no single answer proven by this patch.
A credible proposal names a real database mechanism and tests two interleaving
transactions. A pre-write conflict query alone is not an atomic guarantee.
The accepted review should acknowledge that remaining concern without expanding
a small, testable relationship repair into an unverified scheduling redesign.

## Answers to the new Check items

P4's combination returns `EventType.NotFound`: scope is tested on line 27 and
state on line 29, so an inactive event owned by someone else never reveals
that it is inactive. I2 throws `InvalidOperationException` at line 21 before
any repository call; the exception middleware turns it into a 500 with a
generic `Internal Server Error` body. I3 needs Docker; without the bearer
token the request fails for the reason in finding A, not because of the guard.

## Verified review findings the lab did not fix (checked 2026-10-06)

These are statically verified against the current source. None was executed;
each names the test that would prove it.

**A. The "anonymous" endpoints cannot resolve a tenant without a JWT.**
`TenantResolutionMiddleware.cs:20` stores the resolved tenant in
`context.Items["TenantId"]`, but no code reads that key.
`CurrentTenantService.cs:12-13` reads only the `tenant_id` claim from
`HttpContext.User`. For an invitee with no token, `TenantId` is `null` and
`CreateBookingHandler.cs:21` / `GetAvailableSlotsHandler.cs:20` throw on
`TenantId!.Value`; `ExceptionHandlingMiddleware.cs:31-38` returns 500. The
only success-path integration test hides this by attaching a JWT
(`CreateBookingEndpointTests.cs:26-27`); the invalid-email test passes without
one only because `ValidationBehavior` rejects the command before the handler
runs. Fix direction: have `CurrentTenantService` fall back to
`Items["TenantId"]` (or make the middleware set a claim), then add an
integration test that books with `X-Tenant-Slug` and no token.

**B. A booking conflict maps to 400, not the advertised 409.**
`BookingsController.cs:23` declares `409`. The handler returns code
`Booking.TimeConflict` (`CreateBookingHandler.cs:35`), but
`ResultExtensions.cs:28` maps to 409 only when the code contains `.Conflict`,
`Taken` or `AlreadyCancelled`. `"Booking.TimeConflict"` contains
`.TimeConflict`, not `.Conflict`, so it falls through to `_ => 400` (line 30).
No test posts the same slot twice end to end, so nothing catches it. Fix
direction: map by an explicit error type rather than substring matching, and
add the duplicate-slot integration test.

**C. The conflict query is not tenant-scoped.**
`BookingRepository.cs:16-23` filters by `HostId` only, while `ListByHostAsync`
on lines 25-28 filters by host **and** tenant. With globally unique user IDs
this is harmless today, but the two availability inputs disagree on scope; a
reviewer should ask which one is intended.

**D. Check-then-insert race.** The conflict query (line 33) and the insert
(lines 43-44) share no transaction or constraint;
`BookingConfiguration.cs:51-53` defines no uniqueness or exclusion on host
time ranges. The [course code tour](course/CODE-TOUR.md) §4 develops this.
