# Mid-Level Feature Tickets

Twelve tickets requiring cross-cutting judgment: design decisions, tradeoffs, and changes that touch multiple layers.

---

## TICKET-M01: Implement Tenant IDOR Fix (RISK-001)

**Type:** Security bug fix  
**Effort:** Half day  
**Skills:** Repository layer, DDD, security

**Problem:**  
`RepositoryBase.GetByIdAsync(id)` queries by ID only — no tenant scope. Any authenticated user can read any entity by guessing its GUID. See [`src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs`](../../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs).

**Your task:**  
Add tenant scope to all `GetById` queries. Two approaches:

**Option A — Fix in repository base:**
Add overloads that accept `TenantId` and filter by it.

**Option B — Fix in handlers:**
After loading, check `entity.TenantId == currentTenant.TenantId`. Return 404 if mismatch (not 403 — don't confirm existence).

**Design question to answer first:** Should the repository layer or the application layer be responsible for tenant scope? Write your reasoning in the PR description.

**Files to touch:**
- [`src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs`](../../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs)
- `GetBookingHandler`, `GetEventTypeHandler`, `GetScheduleHandler`

**Definition of done:**
- [ ] Integration test: Tenant A's token cannot access Tenant B's booking by ID
- [ ] Integration test: Own booking still accessible
- [ ] All existing tests pass

---

## TICKET-M02: Add Booking Pagination to `ListByHostAsync`

**Type:** Feature  
**Effort:** Half day  
**Skills:** CQRS query design, EF Core, pagination

**Problem:**  
`GET /api/bookings?hostId=...` returns all bookings with no limit. A host with years of bookings would get an unbounded response.

**Requirements:**
- Add `page` (default: 1) and `pageSize` (default: 25, max: 100) to `GetBookingsQuery`
- Return a `PagedResult<BookingDto>` with `items`, `totalCount`, `page`, `pageSize`
- Pagination must happen in SQL (`.Skip().Take()`), not in memory

**Design decision:** Where should max page size be enforced — validator, handler, or repository?

**Files to touch:**
- `GetBookingsQuery` record
- `GetBookingsHandler`
- `IBookingRepository` + `BookingRepository`
- `BookingsController`
- Add `PagedResult<T>` type in Application Common

**Definition of done:**
- [ ] `GET /api/bookings?page=1&pageSize=5` returns max 5 items
- [ ] `GET /api/bookings?pageSize=9999` is clamped to 100
- [ ] `totalCount` in response is accurate
- [ ] Verified SQL via EF logs: only fetches `pageSize` rows

---

## TICKET-M03: Implement `BookingCancelledEvent` Handler — Audit Log

**Type:** Feature (domain events)  
**Effort:** Half day  
**Skills:** MediatR notifications, DDD side effects

**Problem:**  
`BookingCancelledEvent` is published but has no handler. Implement a handler that writes to an audit log table.

**Schema to create:**
```
booking_audit_log
  id (uuid, PK)
  tenant_id (uuid)
  booking_id (uuid, FK)
  action (varchar: "Cancelled")
  occurred_at (timestamptz)
  reason (varchar, nullable)
```

**Steps:**
1. Create `BookingAuditLog` entity in Domain
2. Create `BookingAuditLogConfiguration` in Infrastructure
3. Create migration
4. Implement `INotificationHandler<BookingCancelledEvent>` in Application
5. Register in DI (MediatR assembly scan handles this automatically)

**Definition of done:**
- [ ] After cancellation, `booking_audit_log` has a row
- [ ] Integration test verifies the row exists after `POST /api/bookings/cancel`

---

## TICKET-M04: Add IANA Timezone Validation (RISK-002)

**Type:** Bug fix  
**Effort:** Half day  
**Skills:** Cross-platform .NET, NuGet packages

**Problem:**  
Timezone strings from users are not validated. `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` throws on Windows; `FindSystemTimeZoneById("Eastern Standard Time")` throws on Linux.

**Solution:**  
Use the `TimeZoneConverter` NuGet package for cross-platform IANA timezone support.

**Install:**
```bash
dotnet add package TimeZoneConverter --version 6.1.0
```

**Add validation in `CreateBookingValidator`:**
```csharp
RuleFor(c => c.InviteeTimezone)
    .Must(tz => TZConvert.TryGetTimeZoneInfo(tz, out _))
    .WithMessage("Timezone is not recognized. Use IANA format (e.g., 'America/New_York').");
```

**Also fix:** `GetAvailableSlotsHandler` — use `TZConvert.GetTimeZoneInfo(timezone)` instead of `TimeZoneInfo.FindSystemTimeZoneById(timezone)`.

**Definition of done:**
- [ ] `InviteeTimezone: "America/New_York"` passes validation on Windows and Linux
- [ ] `InviteeTimezone: "InvalidZone"` returns 400

---

## TICKET-M05: Add Rate Limiting to Public Endpoints (RISK-006)

**Type:** Security feature  
**Effort:** Half day  
**Skills:** ASP.NET Core middleware, rate limiting

**Implement:** ASP.NET Core 8 built-in rate limiting for:
- `POST /api/bookings` — 60 requests/minute per IP
- `GET /api/schedules/available-slots` — 120 requests/minute per IP
- `POST /api/bookings/cancel` — 30 requests/minute per IP (brute-force protection on token)

**Files:**
- [`src/NetCalendy.Api/Program.cs`](../../../src/NetCalendy.Api/Program.cs) — register and configure
- Controllers — add `[EnableRateLimiting("...")]` attribute

**Design decision:** Should rate limits be per-IP, per-tenant, or per-user? Document your reasoning.

**Definition of done:**
- [ ] 61st identical request within 60s returns HTTP 429
- [ ] Rate limit headers (`Retry-After`) present on 429 response

---

## TICKET-M06: Extract `ICurrentUserService` and Fix Claim Access

**Type:** Refactor + hardening  
**Effort:** Half day  
**Skills:** DI, claims, ASP.NET Core

**Problem:**  
Some controllers access JWT claims directly with `User.FindFirst("sub")!.Value`. If `[Authorize]` is removed, this throws instead of failing gracefully.

**Task:**
1. If `ICurrentUserService` doesn't exist, create it in Application layer
2. Implement it in Infrastructure (reads `IHttpContextAccessor`)
3. Register as Scoped
4. Replace all direct claim accesses in controllers with the service

**Interface:**
```csharp
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
}
```

**Definition of done:**
- [ ] No controller directly calls `User.FindFirst("sub")`
- [ ] Handler that requires auth returns clean error if UserId is null

---

## TICKET-M07: Add `GET /api/bookings/{id}` with Tenant Scope

**Type:** Feature  
**Effort:** Half day  
**Skills:** CQRS query, repository, controller

**Problem:**  
If `GetBookingHandler` and `GetBookingsController` GET-by-ID endpoint doesn't exist, create them. If they exist, verify they include tenant scope (see TICKET-M01).

**Requirements:**
- `GET /api/bookings/{id}` returns full `BookingDto` for authenticated user
- Must check tenant scope — return 404 for cross-tenant access
- Must check ownership or role — return 404 if the booking doesn't belong to the requesting user's tenant

**Definition of done:**
- [ ] 200 with correct body for own booking
- [ ] 404 for non-existent booking
- [ ] 404 for another tenant's booking (with own valid token)
- [ ] Integration tests for all three cases

---

## TICKET-M08: Add `GET /api/public/event-types/{slug}` Public Endpoint

**Type:** Feature  
**Effort:** Full day  
**Skills:** Domain design, public routes, multi-tenancy

**Context:**  
The public booking flow (`/[tenant-slug]/[event-type-slug]`) needs to fetch an event type without authentication. This requires a public endpoint that looks up by slug.

**Design challenge:**  
Tenant resolution for a public endpoint. The tenant must come from the URL (the event type slug includes the owner's identity), not the JWT. Decide: does the URL include the tenant slug, or the host's username?

**Proposed URL:** `GET /api/public/{tenantSlug}/{eventTypeSlug}`

**Requirements:**
1. Look up tenant by slug
2. Look up event type by slug within that tenant
3. Return a reduced `PublicEventTypeDto` (no owner ID, no internal IDs beyond what's needed for booking)
4. No authentication required (`[AllowAnonymous]`)

**Files to create:**
- `GetPublicEventTypeQuery` + handler
- `PublicController.cs`
- `PublicEventTypeDto` (different from the authenticated DTO)

---

## TICKET-M09: Write Integration Tests for the Validation Pipeline

**Type:** Test  
**Effort:** Half day  
**Skills:** Integration testing, FluentValidation

**Goal:** Verify that the FluentValidation pipeline (not just domain validation) fires correctly.

**Test scenarios to cover:**
1. Missing required fields → 400 with `ValidationProblemDetails` format
2. Past `startTime` → 400
3. Invalid email format → 400
4. Multiple validation errors in one request → 400 with all errors in `errors` object (not just the first)
5. Valid request → handler executes (happy path)

**Definition of done:**
- [ ] All 5 scenarios have passing integration tests
- [ ] Response body is `ValidationProblemDetails` format (verify `type`, `title`, `errors` fields)

---

## TICKET-M10: Add Serilog Enricher for Tenant and User ID

**Type:** Observability feature  
**Effort:** Half day  
**Skills:** Serilog, middleware, structured logging

**Goal:** Every log line during a request should include `TenantId` and `UserId` as structured properties — without having to manually add them to each log call.

**Implementation:**  
Middleware that pushes these to `LogContext` after authentication:

```csharp
// After UseAuthentication() and UseAuthorization()
app.Use(async (ctx, next) => {
    var tenantId = ctx.User.FindFirst("tenant_id")?.Value;
    var userId = ctx.User.FindFirst("sub")?.Value;
    using (LogContext.PushProperty("TenantId", tenantId))
    using (LogContext.PushProperty("UserId", userId))
    {
        await next();
    }
});
```

**Files:**
- [`src/NetCalendy.Api/Program.cs`](../../../src/NetCalendy.Api/Program.cs)

**Definition of done:**
- [ ] Seq shows `TenantId` and `UserId` on every authenticated request log
- [ ] Anonymous request logs have null values (not missing property)

---

## TICKET-M11: Add Health Check Endpoint

**Type:** Infrastructure feature  
**Effort:** Half day  
**Skills:** ASP.NET Core health checks, PostgreSQL

**Implement:** Two endpoints as described in `06-observability-and-operations.md`.

**Requirements:**
- `/health/live` → returns 200 if process is alive
- `/health/ready` → returns 200 if PostgreSQL is reachable
- Response format: JSON with each check's name, status, and duration

**Definition of done:**
- [ ] `GET /health/live` returns 200
- [ ] `GET /health/ready` returns 200 when DB is up, 503 when DB is down
- [ ] Integration test mocks DB failure and verifies 503

---

## TICKET-M12: Add `NetCalendy.Application.Tests` Project

**Type:** Infrastructure (test)  
**Effort:** Full day  
**Skills:** xUnit, NSubstitute, project setup

**Goal:** Create the missing application-layer unit test project and write 5+ handler tests using mocked repositories.

**Steps:**
1. Create `tests/NetCalendy.Application.Tests/NetCalendy.Application.Tests.csproj`
2. Add NSubstitute, xUnit, FluentAssertions, project reference
3. Add to `.sln`
4. Write tests for:
   - `CreateBookingHandler` — event type not found → failure
   - `CreateBookingHandler` — time conflict → failure
   - `CreateBookingHandler` — happy path → success + booking added + UoW called
   - `CancelBookingHandler` — invalid token → 404
   - `CancelBookingHandler` — already cancelled → 409

**Definition of done:**
- [ ] Project builds and all 5+ tests pass
- [ ] No database, no containers — pure unit tests
