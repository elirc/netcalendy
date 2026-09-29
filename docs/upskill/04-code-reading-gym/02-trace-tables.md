# 02 — Trace Tables

Complete these tables by reading the referenced files. Fill in "Value shape" with what the data looks like at that point (types, structure, or example values). The completed table is your understanding test.

---

## Trace 1: `POST /api/bookings` Happy Path

Starting input: `{ eventTypeId: "abc-...", hostUserId: "def-...", startTime: "2026-07-01T09:00:00Z", inviteeName: "Alice", inviteeEmail: "alice@example.com", inviteeTimezone: "UTC" }`

| Step | File | Line | Owner | What happens | Value shape | Risk |
|---|---|---|---|---|---|---|
| 1 | `BookingsController.cs` | 19 | API | JSON deserialized to `CreateBookingCommand` | _fill in_ | |
| 2 | `LoggingBehavior.cs` | 13 | Pipeline | Log + stopwatch start | _fill in_ | |
| 3 | `ValidationBehavior.cs` | 14 | Pipeline | Validators run | _fill in_ | |
| 4 | `CreateBookingHandler.cs` | 23 | Application | Load EventType | `EventType?` — _fill in null case_ | |
| 5 | `CreateBookingHandler.cs` | 30 | Application | Conflict check | `IReadOnlyList<Booking>` | Race condition |
| 6 | `Invitee.Create(...)` | — | Domain | Value object factory | _fill in_ | |
| 7 | `Booking.Create(...)` | 37 | Domain | Aggregate factory | _fill in_ | |
| 8 | `BookingRepository.Add(booking)` | — | Infrastructure | EF change tracker | _fill in_ | |
| 9 | `AppDbContext.SaveChangesAsync` | 31 | Infrastructure | DB write + event dispatch | _fill in_ | |
| 10 | `ResultExtensions.ToCreatedResult` | — | API | HTTP 201 + body | `CreateBookingResponse` | |

**Answer key verification:** After completing, count: how many database queries execute in this happy path? (Answer: exactly 3 — load EventType, load conflicting bookings, INSERT booking.)

---

## Trace 2: `POST /api/bookings` — Validation Failure

Starting input: `{ inviteeEmail: "not-an-email", startTime: "2020-01-01T00:00:00Z" (past) }`

| Step | File | What happens | Outcome |
|---|---|---|---|
| 1 | `ValidationBehavior.cs` | Runs validators | _fill in — which validators fire?_ |
| 2 | `ValidationBehavior.cs` | Failures found | Throws `ValidationException` with _fill in_ errors |
| 3 | `ExceptionHandlingMiddleware.cs` | Catches exception | Writes HTTP _fill in_ + `ValidationProblemDetails` |
| 4 | Handler | _fill in_ | |

**Question:** Does the handler ever execute? Does any database query run?

---

## Trace 3: JWT Auth Path

Starting input: Bearer token containing `{ sub: "user-guid", tenant_id: "tenant-guid", tenant_slug: "acme" }`

Request: `POST /api/event-types` with `[Authorize]` on the controller.

| Step | File | What happens | Value shape |
|---|---|---|---|
| 1 | `Program.cs` (middleware order) | Request enters pipeline | |
| 2 | `ExceptionHandlingMiddleware` | Wraps the remaining pipeline | |
| 3 | `TenantResolutionMiddleware` | Reads header/subdomain | `HttpContext.Items["TenantId"]` = _fill in_ |
| 4 | `UseAuthentication()` | JWT validated | `HttpContext.User` = _fill in claims_ |
| 5 | `UseAuthorization()` | `[Authorize]` checked | _fill in — what happens if no token?_ |
| 6 | `EventTypesController.Create` | Action executes | |
| 7 | `CurrentTenantService.TenantId` | Reads JWT claim | _fill in — which claim key?_ |
| 8 | `CurrentUserService.UserId` | Reads JWT claim | _fill in — which claim key?_ |

**Question:** If the JWT is valid but the `tenant_id` claim is missing, at what step does this first cause a problem?

---

## Trace 4: Integration Test Lifecycle

Starting point: xUnit discovers `CreateBookingEndpointTests`.

| Step | File | What happens | Duration |
|---|---|---|---|
| 1 | `IntegrationTestWebAppFactory.InitializeAsync` | Postgres container starts | ~3–10s |
| 2 | `WebApplicationFactory.ConfigureWebHost` | DI overridden | <1s |
| 3 | `SeedData()` | Direct DB writes | <1s |
| 4 | `POST_bookings_WithValidSlot_Returns201` | HTTP request via in-process Kestrel | _fill in_ |
| 5 | Full middleware stack | Runs as if production | _fill in_ |
| 6 | Assert | Response status and body checked | |
| 7 | `DisposeAsync` (after all tests in class) | Container stopped | ~1s |

**Question:** If `POST_bookings_WithValidSlot_Returns201` fails, does `POST_bookings_cancel_WithValidToken_Returns200` still run with clean state?

---

## Trace 5: React Query Cache Lifecycle

Starting point: User navigates to `/dashboard`. `DashboardPage` mounts.

| Step | File | What happens | Cache state |
|---|---|---|---|
| 1 | `DashboardPage.tsx` | Component mounts, `useEventTypes()` called | _fill in — is cache empty?_ |
| 2 | `useEventTypes.ts` | `useQuery({ queryKey: ['event-types'], queryFn: eventTypesApi.list })` | `isLoading: true` |
| 3 | `eventTypes.ts:8` | `GET /api/event-types` fires | _fill in_ |
| 4 | `useEventTypes.ts` | Response received | `data: EventType[]`, cached for 30s |
| 5 | User clicks "Delete" on one event type | `useDeleteEventType().mutate(id)` fires | _fill in_ |
| 6 | `useEventTypes.ts:14` | `onSuccess: qc.invalidateQueries(EVENT_TYPES_KEY)` | Cache invalidated |
| 7 | React Query | Re-fetches `GET /api/event-types` automatically | _fill in_ |

**Question:** If the user navigates away from `/dashboard` and returns within 30 seconds, does React Query make a new API call?

---

## Trace 6: Cancel Booking by Token

Request: `POST /api/bookings/cancel` with body `{ cancellationToken: "abc123def456ghij", reason: "Changed plans" }`

| Step | File | Line | What happens | Value shape |
|---|---|---|---|---|
| 1 | `BookingsController.cs` | 56 | Deserialize `CancelBookingRequest` | |
| 2 | `CancelBookingHandler.cs` | 15 | Load booking by token | `Booking?` |
| 3 | `Booking.Cancel(reason)` | 56 | State machine | _fill in — what changes?_ |
| 4 | `bookings.Update(booking)` | — | EF marks entity modified | |
| 5 | `uow.SaveChangesAsync()` | — | SQL UPDATE | _fill in columns changed_ |
| 6 | Domain event dispatched | `AppDbContext.cs:42` | `BookingCancelledEvent` | _fill in — any handler?_ |
| 7 | HTTP response | `ResultExtensions` | `result.ToActionResult()` → _fill in status_ | |

**Question:** What does the caller receive if the `cancellationToken` is valid but the booking is already cancelled?
