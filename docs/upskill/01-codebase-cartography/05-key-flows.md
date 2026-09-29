# 05 — Key Flows

Six end-to-end flows covering the major paths through the system. For each: why it matters, where to start reading, a step-by-step trace, what juniors miss, and what seniors notice.

---

## Flow 1: Create a Booking (Invitee Books a Slot)

**Why it matters:** The core user journey. No authentication required. Demonstrates conflict detection, value object validation, domain event dispatch, and the Result pattern all in one request.

**Open first:** [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
|---|---|---|---|---|---|
| 1 | HTTP | `BookingsController.cs:19` | `[AllowAnonymous]` — no auth check. Deserializes body to `CreateBookingCommand`. | `{ eventTypeId, hostUserId, startTime, inviteeName, inviteeEmail, inviteeTimezone }` | No rate limiting on this endpoint |
| 2 | MediatR pipeline | `LoggingBehavior.cs:13` | Logs `"Handling CreateBookingCommand"`, starts stopwatch | — | — |
| 3 | MediatR pipeline | `ValidationBehavior.cs:14` | Runs `CreateBookingValidator`: email format, future startTime, non-empty name/tz | Throws `ValidationException` if invalid → caught by `ExceptionHandlingMiddleware` | startTime check uses `DateTimeOffset.UtcNow` at validation time, not at create time |
| 4 | Handler | `CreateBookingHandler.cs:23` | Loads `EventType` by ID from `IEventTypeRepository` | `EventType?` — null means 404 | No tenant scoping on this load — cross-tenant booking possible (see risk register) |
| 5 | Handler | `CreateBookingHandler.cs:26` | Checks `eventType.IsActive` | bool | — |
| 6 | Handler | `CreateBookingHandler.cs:30` | Calls `bookings.GetConflictingBookingsAsync(hostId, start, end)` | `IReadOnlyList<Booking>` — checks `Status != Cancelled && StartTime < end && EndTime > start` | Does not lock rows; race condition possible under concurrent requests |
| 7 | Handler | `CreateBookingHandler.cs:35` | Calls `Invitee.Create(name, email, tz, notes)` | `Result<Invitee>` | Email validated again here (already done by FluentValidation — minor duplication) |
| 8 | Handler | `CreateBookingHandler.cs:40` | Calls `Booking.Create(...)` | `Result<Booking>` — raises `BookingCreatedEvent`, generates `CancellationToken` | |
| 9 | Handler | `CreateBookingHandler.cs:43` | `bookings.Add(booking)` — EF change tracker registers the entity | In-memory only | |
| 10 | Handler | `CreateBookingHandler.cs:44` | `uow.SaveChangesAsync()` → `AppDbContext.SaveChangesAsync` | SQL INSERT | Domain events dispatched **after** save succeeds |
| 11 | DbContext | `AppDbContext.cs:31` | Collects domain events, clears them, calls `publisher.Publish` for each | `INotification` via MediatR | No handler for `BookingCreatedEvent` exists yet — events fire but do nothing |
| 12 | Handler | returns | `Result.Success(new CreateBookingResponse(...))` | Response DTO | |
| 13 | Controller | `ResultExtensions.cs:9` | `result.ToCreatedResult(...)` → HTTP 201 | JSON body + Location header | |

**Validation & authorization:**
- FluentValidation: [`CreateBookingValidator.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs)
- Domain invariant: [`Booking.cs:44-47`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L44)
- No auth required (public endpoint)

**Persistence & side effects:**
- [`BookingConfiguration.cs`](../../src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs) — column mapping, `cancellation_token` unique index
- Domain event: `BookingCreatedEvent` dispatched post-save (no handler yet)

**Tests:**
- [`tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs`](../../tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs) — domain invariants
- [`tests/NetCalendy.Application.Tests/Bookings/CreateBookingHandlerTests.cs`](../../tests/NetCalendy.Application.Tests/Bookings/CreateBookingHandlerTests.cs) — handler unit tests with mocks
- [`tests/NetCalendy.Integration.Tests/BookingsEndpoints/CreateBookingEndpointTests.cs`](../../tests/NetCalendy.Integration.Tests/BookingsEndpoints/CreateBookingEndpointTests.cs) — full HTTP tests

**Juniors miss:** That step 10 dispatches domain events after the database write — the events can only fail silently if `publisher.Publish` throws and there's no handler.

**Seniors notice:** Steps 6 and 8 run without a database-level lock. Two concurrent requests for the same slot can both pass the conflict check and both insert bookings. This is a race condition ("double-booking bug") — see risk register entry `RISK-003`.

🔧 **Drill:** Trace what happens when `startTime` is 1 hour in the past. Which step catches it first? Which step is redundant?

**Self-grade:** Basic — can name the steps. Solid — can explain why domain events fire after save. Strong — can identify the double-booking race and propose a fix.

---

## Flow 2: Get Available Slots (Public Query)

**Why it matters:** The most complex query. Pure read path. Requires understanding schedule rules, timezone conversion, and conflict filtering.

**Open first:** [`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
|---|---|---|---|---|---|
| 1 | HTTP | `SchedulesController.cs:22` | `[AllowAnonymous]`. Validates `endDate >= startDate` and range ≤ 60 days in the controller. | Query params | Max-range guard is only in the controller, not the handler |
| 2 | Handler | `GetAvailableSlotsHandler.cs:20` | Loads `EventType` — if null, return empty list | `EventType?` | No tenant scope check |
| 3 | Handler | `GetAvailableSlotsHandler.cs:24` | Loads default `Schedule` for host user (oldest by `CreatedAt`) | `Schedule?` — if null, return empty `[]` | Only loads the first schedule; multiple schedules per user not yet surfaced |
| 4 | Handler | `GetAvailableSlotsHandler.cs:29` | Parses timezone string into `TimeZoneInfo` | Throws `InvalidTimeZoneException` if bad — caught and returns `Error.Validation` | IANA vs Windows timezone names differ on Linux vs Windows (investigate) |
| 5 | Handler | `GetAvailableSlotsHandler.cs:38` | Loads all non-cancelled bookings for host in the date range | `IReadOnlyList<Booking>` | Loads entire list into memory — no pagination |
| 6 | Handler | `GetAvailableSlotsHandler.cs:42-55` | For each date in range: `schedule.GetAvailableSlots(date, duration)` → walk in `durationMinutes` increments, filter past, filter conflicts | `List<AvailableSlotDto>` | O(dates × slots × bookings) — slow for large ranges/many bookings |
| 7 | Handler | returns | `Result.Success(slots)` | JSON array of `{startUtc, endUtc, displayTime}` | |

**Validation & authorization:** Controller-level range guard at [`SchedulesController.cs:27-29`](../../src/NetCalendy.Api/Controllers/SchedulesController.cs#L27). No auth required.

**Persistence:** [`ScheduleRepository.cs:12`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/ScheduleRepository.cs#L12) — uses `Include("_rules")` string-based include to load the `AvailabilityRule` owned collection.

**Tests:** No direct unit test for `GetAvailableSlotsHandler`. [`ScheduleAggregateTests.cs`](../../tests/NetCalendy.Domain.Tests/ScheduleTests/ScheduleAggregateTests.cs) covers `GetAvailableSlots` at the domain level.

**Juniors miss:** Step 3 silently returns empty if the user has no schedule — there's no error, just no slots.

**Seniors notice:** Step 4 — `TimeZoneInfo.FindSystemTimeZoneById` uses different ID formats on Windows (`"Eastern Standard Time"`) vs Linux (`"America/New_York"`). The app uses IANA IDs in the code but this needs `RuntimeInformation.IsOSPlatform` handling or the `TimeZoneConverter` NuGet package. **Investigate before deploying to Linux.**

🔧 **Drill:** What does `GetAvailableSlots` return if the user's schedule has no rule for Monday and `startDate` is a Monday? Trace through [`Schedule.cs:53`](../../src/NetCalendy.Domain/Schedules/Schedule.cs#L53).

---

## Flow 3: Create an Event Type (Authenticated Host Action)

**Why it matters:** Demonstrates the full authentication path: JWT → tenant claim → user claim → handler authorization.

**Open first:** [`src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeHandler.cs`](../../src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeHandler.cs)

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
|---|---|---|---|---|---|
| 1 | HTTP | `EventTypesController.cs:26` | `[Authorize]` — ASP.NET Core validates JWT. 401 if missing/expired. | JWT Bearer token | |
| 2 | Middleware | `TenantResolutionMiddleware.cs:10` | Reads `X-Tenant-Slug` header, queries `ITenantRepository.GetBySlugAsync`, stores `TenantId` in `HttpContext.Items` | `Guid?` | Runs before auth — tenant context always available even for unauthenticated requests |
| 3 | Handler | `CreateEventTypeHandler.cs:16` | Reads `currentTenant.TenantId` from `ICurrentTenantService` (reads JWT claim `tenant_id`) | `Guid?` | If JWT lacks `tenant_id` claim, `currentTenant.TenantId` is null — handler would throw `NullReferenceException` (missing null guard — investigate) |
| 4 | Handler | `CreateEventTypeHandler.cs:19` | Checks slug uniqueness per user within tenant | `bool` | |
| 5 | Handler | `CreateEventTypeHandler.cs:23` | `EventType.Create(tenantId, userId, name, slug, duration, description, color)` | `Result<EventType>` | |
| 6 | Handler | `CreateEventTypeHandler.cs:26` | `eventTypes.Add(et)` + `uow.SaveChangesAsync()` | SQL INSERT | |

**Validation & authorization:** [`CreateEventTypeValidator.cs`](../../src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeValidator.cs) — slug regex, color hex format, duration range.

**Tests:** [`tests/NetCalendy.Application.Tests/EventTypes/CreateEventTypeHandlerTests.cs`](../../tests/NetCalendy.Application.Tests/EventTypes/CreateEventTypeHandlerTests.cs) — slug conflict, successful create. [`tests/NetCalendy.Integration.Tests/EventTypesEndpoints/EventTypesEndpointTests.cs`](../../tests/NetCalendy.Integration.Tests/EventTypesEndpoints/EventTypesEndpointTests.cs) — 401 when unauthenticated, 201 when authenticated.

**Juniors miss:** The difference between `TenantResolutionMiddleware` (reads from header/subdomain, stores in Items) and `CurrentTenantService` (reads from JWT claim). They solve different parts of the tenant resolution problem.

**Seniors notice:** Step 3 has a latent NPE. If the JWT is valid but contains no `tenant_id` claim, `currentTenant.TenantId!.Value` throws. A proper guard + 403 response is missing.

---

## Flow 4: Cancel a Booking (Token-Based, No Auth)

**Why it matters:** Shows the cancellation token pattern, idempotency, and domain aggregate state machine.

**Open first:** [`src/NetCalendy.Domain/Bookings/Booking.cs:56`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L56)

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
|---|---|---|---|---|---|
| 1 | HTTP | `BookingsController.cs:56` | `[AllowAnonymous]`. Deserializes `{cancellationToken, reason}`. | `CancelBookingRequest` | Token is 16 hex chars — weak entropy for security (investigate) |
| 2 | Handler | `CancelBookingHandler.cs:15` | Loads booking by cancellation token | `Booking?` — null means 404 | Token search is via unique index on `cancellation_token` column — fast |
| 3 | Aggregate | `Booking.Cancel()` | Checks `Status == Cancelled` → returns `Error.Conflict`. Otherwise sets status and raises event. | `Result` | |
| 4 | Persistence | `uow.SaveChangesAsync()` | SQL UPDATE | — | Domain event `BookingCancelledEvent` fires post-save (no handler) |

**Juniors miss:** Anyone with the token can cancel the booking. This is by design (Calendly works this way) but the token needs to be unpredictable enough. Current 16-char hex from `Guid.NewGuid()` is ~53 bits of entropy — probably acceptable but worth documenting.

**Seniors notice:** No audit trail beyond the `CancellationReason` column. If you need to know *who* cancelled (host vs invitee), you'd need an additional field or event enrichment.

---

## Flow 5: Integration Test Lifecycle (Testcontainers)

**Why it matters:** Understanding how the integration tests work teaches you WebApplicationFactory, dependency injection override, and Testcontainers. This is the pattern you'll replicate for every new integration test.

**Open first:** [`tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs)

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
|---|---|---|---|---|---|
| 1 | xUnit | Test class `IClassFixture<IntegrationTestWebAppFactory>` | `IntegrationTestWebAppFactory.InitializeAsync()` starts a `PostgreSqlContainer` via Docker | Docker pull + container start (~3–10s first time) | Requires Docker Desktop running |
| 2 | `IntegrationTestWebAppFactory` | `ConfigureWebHost` | Removes the real `DbContextOptions<AppDbContext>` DI registration, replaces with one pointing to the test container's connection string | DI override | |
| 3 | Test | `SeedData()` | Creates scope, gets `AppDbContext`, adds test entities, calls `SaveChangesAsync()` | Direct DB writes bypassing handlers | Seed bypasses domain invariants — possible inconsistent test state |
| 4 | Test | `factory.CreateClient()` | `HttpClient` that routes to the in-process Kestrel | HTTP client | |
| 5 | Test | `_client.PostAsJsonAsync(...)` | Real HTTP request through full middleware stack | HTTP request/response | Auth: `JwtTokenFactory.GenerateToken()` creates a valid JWT signed with the same key as the app's config |
| 6 | xUnit | `DisposeAsync()` | Stops and removes the Postgres container | — | Container not reset between tests in same class — test isolation relies on unique slugs/emails in seed data |

**Juniors miss:** Why the test generates a real JWT (`JwtTokenFactory`) instead of mocking auth — because `WebApplicationFactory` runs the full real middleware, so mocking auth is harder and less valuable than using a real token.

**Seniors notice:** Tests in `CreateBookingEndpointTests` and `EventTypesEndpointTests` share one factory instance per class but **do not reset the database between test methods**. Test isolation depends on using unique slugs and emails per test. If a test fails mid-way, state may pollute the next test.

---

## Flow 6: React Booking UI (Frontend Flow)

**Why it matters:** Shows React Query data fetching, multi-step component state, and how the frontend talks to the API.

**Open first:** [`web/src/components/booking/BookingPage.tsx`](../../web/src/components/booking/BookingPage.tsx)

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
|---|---|---|---|---|---|
| 1 | Router | `App.tsx:16` | `/book/:hostSlug/:eventSlug` matches → `BookPage.tsx` rendered | Route params | |
| 2 | `BookPage` | `BookPage.tsx:10` | `useQuery` fetches event type by `eventSlug` (Note: currently uses ID not slug — investigate) | `EventType` | Query key: `['public-event-type', eventSlug]` |
| 3 | React Query | `web/src/api/eventTypes.ts:8` | `GET /api/event-types/{id}` via axios | `EventType` | Requires auth (`[Authorize]` on that endpoint) — **bug**: public booking page can't fetch event type without a token |
| 4 | `BookingPage` | `BookingPage.tsx:26` | `useAvailableSlots(eventTypeId, hostUserId, weekStart)` | `AvailableSlot[]` | |
| 5 | React Query | `web/src/hooks/useAvailableSlots.ts` | `GET /api/schedules/available-slots?eventTypeId=...` | Slots grouped by day | |
| 6 | UI | `BookingPage.tsx:65` | Step `pick-slot`: renders slots by day | — | |
| 7 | User | — | Clicks a slot → `setSelected(slot)`, `setStep('fill-form')` | `AvailableSlot` | |
| 8 | UI | `BookingPage.tsx:46` | Step `fill-form`: form with name/email/notes | `BookingForm` state | |
| 9 | Submission | `BookingPage.tsx:30` | `bookingsApi.create(payload)` → `POST /api/bookings` | `CreateBookingPayload` | |
| 10 | UI | `BookingPage.tsx:38` | Step `confirmed`: shows cancellation link | `cancellationToken` | |

**Tests:** No frontend tests in the repo currently.

**Juniors miss:** Step 3 — `[Authorize]` on `GET /api/event-types/:id` blocks public booking. This is a real bug: the booking page flow is broken end-to-end. The endpoint should be `[AllowAnonymous]` for public event type reads.

**Seniors notice:** The route uses `eventSlug` as a URL param but passes it to a GUID-based API. A proper public read endpoint would accept `/{userSlug}/{eventSlug}` and resolve through the database. The current `BookPage.tsx` uses the event type ID as the "slug" — this is a placeholder that needs a proper public event type resolution endpoint.

🔧 **Drill:** Trace what happens when `useAvailableSlots` returns an empty array. What does the user see? Which code path handles it?
