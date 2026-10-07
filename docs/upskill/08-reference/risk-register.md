# Risk Register

Formalized risks identified during code review and documentation authoring. Each risk has an ID, severity, location, impact, proposed fix, and a test strategy that would verify the fix.

Last reviewed: 2026-06-08

---

## RISK-001: IDOR on GetById — No Tenant Scope Filtering

**Severity:** High (security)  
**Status:** Open

**Location:**  
[`src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs`](../../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs) — `GetByIdAsync` queries by ID only.

Affected handlers: `GetBookingHandler`, `GetEventTypeHandler`, `GetScheduleHandler`.

**Impact:**  
Any authenticated user with a valid JWT can read any entity from any tenant by guessing or enumerating GUIDs. This is a data breach in a multi-tenant SaaS.

**Reproduction:**
1. Tenant A creates a booking → gets `bookingId = abc-123`
2. Tenant B's user sends `GET /api/bookings/abc-123` with their own valid token
3. Tenant A's booking data is returned

**Proposed fix:**  
After loading by ID, check `entity.TenantId == currentTenant.TenantId`. Return 404 on mismatch (not 403 — don't confirm existence).

**Fix ticket:** TICKET-M01

**Test strategy:**
```csharp
// Integration test
[Fact]
public async Task GetBooking_WithDifferentTenantToken_Returns404()
{
    var booking = await _factory.SeedBookingForTenant(tenantAId);
    var tenantBToken = JwtTokenFactory.GenerateToken(tenantBUserId, tenantBId);

    var response = await _client.GetAsync($"/api/bookings/{booking.Id}",
        bearer: tenantBToken);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

---

## RISK-002: Timezone String Not IANA-Validated — Cross-Platform Failure

**Severity:** Medium (correctness)  
**Status:** Open

**Location:**  
[`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs) — `TimeZoneInfo.FindSystemTimeZoneById(timezone)`.

Also: [`src/NetCalendy.Domain/Bookings/Invitee.cs`](../../../src/NetCalendy.Domain/Bookings/Invitee.cs) — stores timezone string without format validation.

**Impact:**  
On Windows, `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` (IANA format) throws `TimeZoneNotFoundException`. On Linux/Docker, `FindSystemTimeZoneById("Eastern Standard Time")` (Windows format) throws. Production containers are typically Linux — any user who uses Windows timezone IDs from a Windows client gets a 500.

**Proposed fix:**  
Add `TimeZoneConverter` NuGet package. Replace `FindSystemTimeZoneById` with `TZConvert.GetTimeZoneInfo`. Add validator rule that checks `TZConvert.TryGetTimeZoneInfo(tz, out _)`.

**Fix ticket:** TICKET-M04

**Test strategy:**
```csharp
// Unit test — with TimeZoneConverter
[Theory]
[InlineData("America/New_York")]     // IANA — works on all platforms
[InlineData("Eastern Standard Time")] // Windows — works via TZConvert on all platforms
public void GetAvailableSlots_WithValidTimezone_Succeeds(string tz) { ... }
```

---

## RISK-003: Double-Booking Race Condition

**Severity:** High (correctness)  
**Status:** Open

**Location:**  
[`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs:30`](../../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs#L30) — conflict check and INSERT are not atomic.

**Impact:**  
Two simultaneous `POST /api/bookings` requests for the same host + time slot both pass the conflict check and both succeed. The host is double-booked. Probability increases with load.

**Proposed fix (Option A):** PostgreSQL exclusion constraint:
```sql
CREATE EXTENSION btree_gist;
ALTER TABLE bookings ADD CONSTRAINT no_overlap
  EXCLUDE USING gist (
    host_id WITH =,
    tstzrange(start_time, end_time) WITH &&
  )
  WHERE (status != 'Cancelled');
```
Handle `UniqueConstraintException` from EF Core and return 409.

**Proposed fix (Option B):** Advisory lock per host before conflict check.

**Fix ticket:** PROJECT-S02

**Test strategy:**
```csharp
// Concurrent integration test
var tasks = Enumerable.Range(0, 10)
    .Select(_ => _client.PostAsJsonAsync("/api/bookings", sameSlotRequest));
var responses = await Task.WhenAll(tasks);
responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(9);
```

---

## RISK-004: Domain Events Fire with No Handlers — Silent No-Op

**Severity:** Low (current) / Medium (when handlers are added)  
**Status:** Accepted (for now)

**Location:**  
[`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs:42`](../../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs#L42) — `publisher.Publish(domainEvent)` succeeds with no handlers (MediatR no-ops).

**Impact:**  
Currently harmless — no handler exists, so the no-op is intentional scaffolding. Risk activates when a handler is added: the handler runs synchronously in the HTTP request, blocking the response, with no retry on failure.

**Proposed fix:** Transactional outbox pattern. See PROJECT-S01.

**Test strategy:**  
When a handler is added, test that it runs even if the process crashes between DB write and publish (requires the outbox to be implemented first).

---

## RISK-005: `[Authorize]` on GET Event Type Breaks Public Booking Flow

**Severity:** Medium (product correctness)  
**Status:** Open

**Location:**  
[`src/NetCalendy.Api/Controllers/EventTypesController.cs`](../../../src/NetCalendy.Api/Controllers/EventTypesController.cs) — class-level `[Authorize]` applies to `GET /api/event-types/{id}`.

**Impact:**  
The public booking flow needs to display event type details (name, duration, description) before a guest books. If `GET /api/event-types/{id}` requires authentication, the booking page can't load for unauthenticated guests.

**Proposed fix:**  
Create a separate public endpoint: `GET /api/public/{tenantSlug}/{eventSlug}` with `[AllowAnonymous]`. The public endpoint returns a reduced DTO (no internal IDs, no owner info beyond what's needed).

**Fix ticket:** TICKET-M08

---

## RISK-006: No Rate Limiting on Public Endpoints

**Severity:** Medium (availability)  
**Status:** Open

**Location:**  
[`src/NetCalendy.Api/Program.cs`](../../../src/NetCalendy.Api/Program.cs) — no `AddRateLimiter` configured.

**Impact:**  
- `POST /api/bookings/cancel` with random tokens = brute-force attack on cancellation tokens
- `GET /api/schedules/available-slots` = easy to scrape all hosts' availability
- `POST /api/bookings` flood = DoS for a specific host

**Proposed fix:**  
ASP.NET Core 8 built-in rate limiter. See TICKET-M05.

---

## RISK-007: JWT Secret in `appsettings.json` — Placeholder Not Enforced

**Severity:** Critical if deployed without override  
**Status:** Open (documentation workaround only)

**Location:**  
[`src/NetCalendy.Api/appsettings.json`](../../../src/NetCalendy.Api/appsettings.json): `"Key": "REPLACE_WITH_A_SECRET_KEY_AT_LEAST_32_CHARS"`

**Impact:**  
If deployed without overriding this value, all tokens are signed with a known key. Any attacker can forge tokens for any tenant or user.

**Current mitigation:**  
Documentation (TICKET-013) and the `appsettings.json` comment. Not enforced by code.

**Proposed fix:**  
At startup, validate that `Jwt:Key` is not the placeholder:
```csharp
var key = config["Jwt:Key"];
if (string.IsNullOrEmpty(key) || key.StartsWith("REPLACE_"))
    throw new InvalidOperationException("Jwt:Key must be set to a real secret before running.");
```

---

## Risk Register Summary

| ID | Severity | Status | Type |
|---|---|---|---|
| RISK-001 | High | Open | Security |
| RISK-002 | Medium | Open | Correctness |
| RISK-003 | High | Open | Correctness |
| RISK-004 | Low→Medium | Accepted | Reliability |
| RISK-005 | Medium | Open | Product |
| RISK-006 | Medium | Open | Availability |
| RISK-007 | Critical | Open | Security |
