# 06 — Architecture Critique

**Status of claims:** Confirmed = inspected directly. Hypothesis = inferred from patterns. Investigate = needs verification.

---

## Strongest Choices

### 1. Clean Architecture Dependency Rule — Confirmed
The inward dependency rule is correctly enforced. Domain has no EF Core, HTTP, or MediatR handler imports. Application has no `HttpContext`. All boundary-crossings go through interfaces. Consequence: you can replace PostgreSQL with SQLite for tests, replace the MediatR pipeline with a raw handler call, or swap controllers for gRPC — without touching domain or application logic.

### 2. Result<T> Railway Pattern — Confirmed
Error handling is explicit, typed, and composable. The `Error` record carries a machine-readable `Code` and human-readable `Description`. The HTTP mapping in `ResultExtensions` is a single place to maintain the code → status mapping. This eliminates the "swallowed exception" class of bugs for expected failure paths.

### 3. Testcontainers Integration Tests — Confirmed
Integration tests start a real Postgres container. No mocking of the database layer. This catches EF Core query issues, missing indexes, migration errors, and SQL-level constraint violations that mock-based tests miss entirely.

### 4. Strong-Typed IDs — Confirmed
Prevents argument-order bugs at compile time. The `record` wrapping pattern is low-overhead (struct-like semantics). Value converters correctly map to `Guid` columns.

### 5. Colocated CQRS Feature Folders — Confirmed
Each feature's Command/Handler/Validator lives in one folder. Finding the code for "create event type" means opening one folder. This scales well as the feature set grows.

---

## Risks and Tradeoffs (Prioritized)

### RISK-001: No Tenant Isolation on `GetById` — HIGH IMPACT, CONFIRMED
**Evidence:** [`src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs:9`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs#L9) — `FindAsync([id], ct)` loads by primary key with no tenant filter.

**Impact:** Any authenticated user who knows (or guesses) another tenant's booking ID can call `GET /api/bookings/{id}` and retrieve it. This is an IDOR (Insecure Direct Object Reference) vulnerability.

**Root cause:** `RepositoryBase.GetByIdAsync` has no tenant scope. Handlers call it without adding a post-load tenant check.

**Suggested fix:** Add `ValidateTenantOwnership(aggregate, tenantId)` in each handler after loading, or add an overload `GetByIdAsync(TId id, TenantId tenantId)` that enforces scope. Alternatively, add EF global query filters per entity.

**Test strategy:** Integration test: create booking for TenantA. Authenticate as TenantB user. `GET /api/bookings/{bookingId}`. Assert 403 or 404, not 200.

---

### RISK-002: Timezone String Not IANA-Validated — MEDIUM IMPACT, CONFIRMED
**Evidence:** `User.Create()` and `Invitee.Create()` accept any non-empty string as timezone. `GetAvailableSlotsHandler.cs:29` calls `TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone)` which throws on Linux with Windows timezone IDs (`"Eastern Standard Time"` instead of `"America/New_York"`).

**Impact:** On a Linux deployment (Docker/cloud), the `/api/schedules/available-slots` endpoint returns a 400 error for hosts with Windows-format timezone strings.

**Suggested fix:** Use the `TimeZoneConverter` NuGet package (`TZConvert.GetTimeZoneInfo(...)`) which handles both formats cross-platform. Validate timezone in domain/validator against a known list.

---

### RISK-003: Double-Booking Race Condition — MEDIUM IMPACT, HYPOTHESIS
**Evidence:** [`CreateBookingHandler.cs:30`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs#L30) — conflict check reads existing bookings; no database-level lock taken between the read and the insert.

**Impact:** Two concurrent booking requests for the same slot can both pass the conflict check and both succeed, resulting in a double-booking.

**Suggested fix (short term):** Add a unique index on `(host_id, start_time)` for non-cancelled bookings (partial index: `WHERE status != 'Cancelled'`). Let the DB constraint enforce uniqueness; catch the constraint violation and return a 409.

**Test strategy:** Parallel integration test: fire 2 concurrent `POST /api/bookings` for the same slot. Assert only 1 succeeds.

---

### RISK-004: Domain Events Fire with No Handlers — LOW IMPACT NOW, HIGH RISK LATER — CONFIRMED
**Evidence:** No `INotificationHandler<BookingCreatedEvent>` exists anywhere. Events are dispatched via `publisher.Publish` but have zero consumers.

**Impact now:** Events fire and are silently dropped (MediatR publishes to zero subscribers without error).

**Impact later:** When notification handlers are added, they run synchronously in the same HTTP request. A slow email send (3 seconds) adds 3 seconds to the booking creation latency. If the handler throws, the booking is already saved but the side effect failed silently.

**Suggested fix:** Add an outbox table or use a message queue (e.g., Hangfire background jobs) so side effects are durable and async.

---

### RISK-005: `[Authorize]` on Public Event Type Read — MEDIUM IMPACT, CONFIRMED
**Evidence:** [`EventTypesController.cs:9`](../../src/NetCalendy.Api/Controllers/EventTypesController.cs#L9) — class-level `[Authorize]`. `GET /api/event-types/{id}` is on this controller with no `[AllowAnonymous]`.

**Impact:** The public booking page (`BookPage.tsx`) calls this endpoint to load the event type. It will fail with 401 for unauthenticated visitors. The public booking flow is broken.

**Suggested fix:** Add `[AllowAnonymous]` to `GetById`, or create a separate public endpoint `GET /api/public/{userSlug}/{eventSlug}` with proper slug-based resolution.

---

### RISK-006: No Rate Limiting on Public Endpoints — LOW IMPACT NOW — HYPOTHESIS
**Evidence:** `POST /api/bookings` and `GET /api/schedules/available-slots` are `[AllowAnonymous]` with no rate limiting middleware visible in `Program.cs`.

**Impact:** Scrapers can enumerate available slots. Bots can create bookings. No protection against DoS via slot flooding.

**Suggested fix:** Add `AddRateLimiter()` in `Program.cs` with a sliding window policy on public endpoints.

---

## "If I Owned This for 3 Months"

Priority 1 (before any external users): Fix RISK-001 (IDOR) and RISK-005 (broken public booking flow).

Priority 2 (before production traffic): RISK-003 (double-booking) — add unique partial index.

Priority 3 (first 30 days): RISK-002 (timezone) — add `TimeZoneConverter`, validate on input.

Priority 4 (month 2): RISK-004 — design outbox or Hangfire pattern for domain event side effects (email notifications).

Priority 5 (month 3): RISK-006 — rate limiting, plus `global.json` for SDK pinning, `UpdateEventTypeCommand` validator, and EF global query filters for defense-in-depth on tenant isolation.

---

## Migration and Test Strategy per Recommendation

### Adding EF Global Query Filters (RISK-001 defense-in-depth)
1. Add `builder.HasQueryFilter(...)` to each tenant-scoped entity configuration
2. Requires `ICurrentTenantService` in `AppDbContext` — it's already injected
3. Disable global filter selectively with `.IgnoreQueryFilters()` for admin queries
4. Test: integration test with two tenants; assert cross-tenant query returns empty
5. Risk: any existing `GetById` that returns null (not 403) now looks like "not found" — document the behavior difference

### Adding Unique Partial Index for Bookings (RISK-003 fix)
1. Add to `BookingConfiguration`: `builder.HasIndex(b => new { b.HostId, b.StartTime }).HasFilter("status != 2")` (2 = Cancelled)
2. Generate new migration: `dotnet ef migrations add AddBookingUniqueSlotIndex`
3. Test: the existing parallel-booking integration test (a ticket to write)
4. In the handler: catch `DbUpdateException` with a Postgres unique violation code (23505) and return `Error.Conflict("Booking.TimeConflict", ...)`
