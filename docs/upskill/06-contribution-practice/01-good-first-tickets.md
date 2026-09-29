# Good First Tickets

Twenty tickets designed for a junior engineer making their first contributions to this codebase. Each is self-contained, has a clear definition of done, and points to exactly where to make the change.

---

## TICKET-001: Add Missing Validator for `UpdateEventTypeCommand`

**Type:** Bug fix  
**Effort:** 1–2 hours  
**Skills:** FluentValidation, Application layer

**Description:**  
`UpdateEventTypeCommand` has no `AbstractValidator` registered. A user can send an empty name or negative duration and the handler will attempt to save it. The domain will catch some of these (via `Duration.Create`), but the user gets a 500 instead of a 400.

**Files to read first:**
- [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs) — follow this pattern exactly
- [`src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeCommand.cs`](../../src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeCommand.cs) — the command you're validating

**What to create:**  
`src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeValidator.cs`

**Rules to add:**
- `Name`: not empty, max 100 chars
- `Description`: max 500 chars (optional, can be null)
- `DurationMinutes`: between 15 and 480 (8 hours max)
- `Color`: matches hex color regex `^#[0-9A-Fa-f]{6}$` (if provided)

**Definition of done:**
- [ ] Validator file exists and is an `AbstractValidator<UpdateEventTypeCommand>`
- [ ] `dotnet test` passes
- [ ] Manual test: `PUT /api/event-types/{id}` with empty name returns HTTP 400 with validation errors

---

## TICKET-002: Return 404 When Event Type Not Found in Update Handler

**Type:** Bug fix  
**Effort:** 30 minutes  
**Skills:** Reading handler code, Result pattern

**Description:**  
`UpdateEventTypeHandler` likely returns an error if the event type isn't found, but review the actual error code. It should return `Error.NotFound("EventType", id)` to produce a 404 response. Verify this is the case, and fix if not.

**Files to read:**
- [`src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeHandler.cs`](../../src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeHandler.cs)
- [`src/NetCalendy.Api/Extensions/ResultExtensions.cs`](../../src/NetCalendy.Api/Extensions/ResultExtensions.cs) — how error codes map to HTTP status

**Definition of done:**
- [ ] `PUT /api/event-types/{nonexistent-id}` returns HTTP 404
- [ ] Test added: integration test asserts 404 for unknown ID

---

## TICKET-003: Add `X-Request-Id` Header to All Responses

**Type:** Feature (observability)  
**Effort:** 1–2 hours  
**Skills:** ASP.NET Core middleware, HTTP headers

**Description:**  
Add a middleware that generates (or reads from the incoming `X-Request-Id` header) a correlation ID and attaches it to every response. This enables tracing a specific request through logs.

**Files to read first:**
- [`src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs`](../../src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs) — use as middleware template
- [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs) — where to register the middleware

**What to create:**  
`src/NetCalendy.Api/Middleware/CorrelationIdMiddleware.cs`

**Behavior:**
1. Check for incoming `X-Request-Id` header
2. If present, use that value; if not, generate `Guid.NewGuid().ToString()`
3. Add the ID to `HttpContext.Items["RequestId"]`
4. Add it to the response header: `context.Response.Headers["X-Request-Id"] = requestId`
5. Push it to Serilog context: `LogContext.PushProperty("RequestId", requestId)`

**Definition of done:**
- [ ] Every response has `X-Request-Id` header
- [ ] Serilog logs include `RequestId` property

---

## TICKET-004: Validate `Color` Field in `CreateEventTypeCommand`

**Type:** Bug fix  
**Effort:** 30 minutes  
**Skills:** FluentValidation, regex

**Description:**  
`CreateEventTypeCommand` accepts a `color` string. Currently there's no validation — a user can pass any string. It should be validated to be a valid hex color code: `#RRGGBB`.

**Files:**
- [`src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeValidator.cs`](../../src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeValidator.cs)

**Add rule:**
```csharp
RuleFor(c => c.Color)
    .Matches(@"^#[0-9A-Fa-f]{6}$")
    .WithMessage("Color must be a valid hex color code (e.g., #FF5733).")
    .When(c => c.Color is not null);
```

**Definition of done:**
- [ ] `POST /api/event-types` with `"color": "red"` returns 400
- [ ] `POST /api/event-types` with `"color": "#FF5733"` passes validation

---

## TICKET-005: Add `isActive` Filter to `GET /api/event-types`

**Type:** Feature  
**Effort:** 2–3 hours  
**Skills:** Query parameters, EF Core filtering

**Description:**  
`GET /api/event-types` returns all event types. Add an optional `?isActive=true/false` query parameter to filter by active status.

**Files to touch:**
- [`src/NetCalendy.Application/EventTypes/Queries/ListEventTypes/ListEventTypesQuery.cs`](../../src/NetCalendy.Application/EventTypes/Queries/ListEventTypes/ListEventTypesQuery.cs) — add optional `bool? IsActive` parameter
- [`src/NetCalendy.Application/EventTypes/Queries/ListEventTypes/ListEventTypesHandler.cs`](../../src/NetCalendy.Application/EventTypes/Queries/ListEventTypes/ListEventTypesHandler.cs) — apply filter if present
- [`src/NetCalendy.Api/Controllers/EventTypesController.cs`](../../src/NetCalendy.Api/Controllers/EventTypesController.cs) — pass query param

**Definition of done:**
- [ ] `GET /api/event-types?isActive=true` returns only active types
- [ ] `GET /api/event-types` (no filter) returns all types
- [ ] Integration test verifies filter behavior

---

## TICKET-006: Fix Typo in Error Message

**Type:** Polish  
**Effort:** 15 minutes  
**Skills:** Finding strings in code

**Description:**  
Search the codebase for error messages that have typos, unclear phrasing, or overly technical language. Fix at least one. Document what you changed and why.

**Search strategy:**
```
grep -r "WithMessage" src/NetCalendy.Application/
```

**Good improvements to look for:**
- Messages that mention internal type names (e.g., "BookingStatus" instead of "booking status")
- Messages that say "must not be empty" vs. "is required" (be consistent)
- Messages that include a stack trace or internal code

**Definition of done:**
- [ ] At least one message improved
- [ ] All validators still pass their tests

---

## TICKET-007: Add `Reason` Field to Cancel Booking Response

**Type:** Feature  
**Effort:** 1 hour  
**Skills:** Response shaping, DTO changes

**Description:**  
`POST /api/bookings/cancel` accepts a `reason` but the response doesn't include it. Update the response to include `cancellationReason`.

**Files:**
- [`src/NetCalendy.Application/Bookings/Commands/CancelBooking/CancelBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CancelBooking/CancelBookingHandler.cs)
- The response record — add `CancellationReason` property

**Definition of done:**
- [ ] Response body includes `cancellationReason` when cancellation succeeds

---

## TICKET-008: Unit Test — `Schedule.GetAvailableSlots` Returns Empty for Non-Workday

**Type:** Test  
**Effort:** 1–2 hours  
**Skills:** Writing domain unit tests

**Description:**  
`Schedule.GetAvailableSlots(date, minutes)` should return an empty list if `date` is a weekend (assuming default Mon–Fri rules). Write a unit test that verifies this.

**Files:**
- [`tests/NetCalendy.Domain.Tests/`](../../tests/NetCalendy.Domain.Tests/) — add test file
- [`src/NetCalendy.Domain/Schedules/Schedule.cs`](../../src/NetCalendy.Domain/Schedules/Schedule.cs) — method to test

**Definition of done:**
- [ ] Test passes with a Saturday input
- [ ] Test passes with a Sunday input
- [ ] Test verifies non-empty result for a weekday

---

## TICKET-009: Add Max Length to Event Type Name in Domain

**Type:** Hardening  
**Effort:** 30 minutes  
**Skills:** Value objects, domain invariants

**Description:**  
`EventType.Name` has a max length enforced in EF configuration (column length). But the domain itself doesn't enforce this. A 1000-character name would fail with a DB truncation error, not a clear domain error.

**Files:**
- [`src/NetCalendy.Domain/EventTypes/EventType.cs`](../../src/NetCalendy.Domain/EventTypes/EventType.cs)
- `EventTypeName` value object if it exists, or add the check in `EventType.Create()`

**Add:** If `name.Length > 100`, return `Result.Failure(Error.Validation("EventType.NameTooLong", "Name must be 100 characters or fewer."))`.

**Definition of done:**
- [ ] Domain unit test: `Create` with 101-char name returns failure
- [ ] Domain unit test: `Create` with 100-char name succeeds

---

## TICKET-010: Integration Test — Unauthorized Access Returns 401

**Type:** Test  
**Effort:** 1 hour  
**Skills:** Integration tests, HTTP headers

**Description:**  
Add integration tests that verify protected endpoints return 401 when called without a token.

**File to add tests in:** `tests/NetCalendy.Integration.Tests/Endpoints/AuthTests.cs`

**Test cases:**
- `POST /api/bookings` without token → 401
- `GET /api/event-types` without token → 401
- `POST /api/event-types` without token → 401
- `POST /api/bookings/cancel` without token → 200 (anonymous, no token needed)

**Definition of done:**
- [ ] All 4 assertions pass

---

## TICKET-011: Add `CreatedAt` to `BookingDto`

**Type:** Feature  
**Effort:** 1 hour  
**Skills:** DTO mapping, EF auditing

**Description:**  
The `Booking` entity should have a `CreatedAt` property. Add it (if not already present) and include it in `BookingDto`.

**Files:**
- [`src/NetCalendy.Domain/Bookings/Booking.cs`](../../src/NetCalendy.Domain/Bookings/Booking.cs) — add `public DateTimeOffset CreatedAt { get; private set; }`
- Set it in the `Create` factory: `CreatedAt = DateTimeOffset.UtcNow`
- [`src/NetCalendy.Application/Bookings/Queries/GetBooking/BookingDto.cs`](../../src/NetCalendy.Application/Bookings/Queries/GetBooking/BookingDto.cs) — add the field

**Note:** Adding a column requires an EF migration. Document the `dotnet ef migrations add` command to run.

**Definition of done:**
- [ ] `GET /api/bookings/{id}` response includes `createdAt`

---

## TICKET-012: Return Sorted Results from `ListEventTypes`

**Type:** Feature  
**Effort:** 30 minutes  
**Skills:** EF Core ordering

**Description:**  
`ListEventTypesHandler` returns event types in database insertion order. Sort them by `Name` ascending.

**File:**
- [`src/NetCalendy.Application/EventTypes/Queries/ListEventTypes/ListEventTypesHandler.cs`](../../src/NetCalendy.Application/EventTypes/Queries/ListEventTypes/ListEventTypesHandler.cs) or the repository method

**Add:** `.OrderBy(e => e.Name)` to the query.

**Definition of done:**
- [ ] `GET /api/event-types` returns names in alphabetical order
- [ ] Integration test: seed 3 event types out of order, verify alphabetical response

---

## TICKET-013: Document the `Jwt:Key` Configuration Requirement

**Type:** Docs / DevEx  
**Effort:** 30 minutes  
**Skills:** Configuration, security

**Description:**  
`appsettings.json` contains a placeholder JWT key that must be replaced. A new developer who clones the repo won't know this. Add a comment in `appsettings.json` and update `README.md` with setup instructions.

**Files:**
- [`src/NetCalendy.Api/appsettings.json`](../../src/NetCalendy.Api/appsettings.json)
- Root `README.md`

**Add instructions for:**
```bash
dotnet user-secrets init --project src/NetCalendy.Api
dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-chars" --project src/NetCalendy.Api
```

**Definition of done:**
- [ ] `README.md` has a "Getting Started" section with the secret setup steps

---

## TICKET-014: Add `BookingStatus` to `CreateBookingResponse`

**Type:** Feature  
**Effort:** 30 minutes  
**Skills:** DTO mapping

**Description:**  
`CreateBookingResponse` doesn't include the initial `BookingStatus`. Add it so callers can confirm the booking is in `Scheduled` state.

**Files:**
- [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs) — update response construction
- `CreateBookingResponse` record — add `Status` field

**Definition of done:**
- [ ] Response includes `"status": "Scheduled"` (or the string representation)

---

## TICKET-015: Integration Test — Cancel with Invalid Token Returns 404

**Type:** Test  
**Effort:** 1 hour  
**Skills:** Integration tests

**Description:**  
`POST /api/bookings/cancel` with a random/invalid cancellation token should return 404. Write this test.

**Definition of done:**
- [ ] Test passes: `{ "cancellationToken": "invalid-token-xyz" }` → HTTP 404

---

## TICKET-016: Add `slug` Uniqueness Validation to `CreateEventTypeValidator`

**Type:** Bug fix  
**Effort:** 30 minutes  
**Skills:** FluentValidation

**Description:**  
Event type slugs must be unique per host. Currently this is enforced only at the DB level (unique index). A duplicate slug causes a DB exception and a 500 response. Add a `.Must(BeUniqueSlug)` rule (async) to the validator.

**Note:** This requires injecting `IEventTypeRepository` into the validator. See how `AbstractValidator` handles async rules with `MustAsync`.

**Definition of done:**
- [ ] Duplicate slug returns HTTP 400, not 500

---

## TICKET-017: Fix `ResultExtensions` — `Forbidden` Should Map to 403

**Type:** Bug fix  
**Effort:** 30 minutes  
**Skills:** HTTP status codes, extension methods

**Description:**  
[`src/NetCalendy.Api/Extensions/ResultExtensions.cs`](../../src/NetCalendy.Api/Extensions/ResultExtensions.cs) maps error codes to HTTP status. Errors with suffix `Forbidden` currently fall through to the default 400 case. They should return 403.

**Fix:** Add a case:
```csharp
"Forbidden" => Results.Forbid(), // 403
```
or map to 403 explicitly.

**Definition of done:**
- [ ] `Error.Validation("EventType.Forbidden", "...")` → HTTP 403
- [ ] Existing test or add one

---

## TICKET-018: Add `dotnet watch` Instructions to README

**Type:** DevEx  
**Effort:** 30 minutes  
**Skills:** .NET tooling

**Description:**  
Document how to run the API with hot reload in development.

**Add to README:**
```bash
cd src/NetCalendy.Api
dotnet watch run
```
Explain: `dotnet watch` recompiles and restarts on file changes. No Docker restart needed.

---

## TICKET-019: Add Null Guard to `ICurrentTenantService` Claim Access

**Type:** Hardening  
**Effort:** 30 minutes  
**Skills:** Null safety, .NET

**Description:**  
`ICurrentTenantService.TenantId` is `Guid?`. In handlers, it's accessed with `currentTenant.TenantId!.Value` — the `!` suppresses the nullable warning. If `[Authorize]` is accidentally removed from an endpoint, this line will throw `NullReferenceException` instead of returning a clear 401.

Add a null check:
```csharp
if (currentTenant.TenantId is null)
    return Result.Failure<T>(Error.Validation("Auth.MissingTenantClaim", "Tenant context is required."));
```

**Files to update:** `CreateBookingHandler`, `CreateEventTypeHandler`, and all other handlers that access `currentTenant.TenantId!.Value`.

---

## TICKET-020: Add Response Caching to `GET /api/schedules/available-slots`

**Type:** Feature (performance)  
**Effort:** 2–3 hours  
**Skills:** ASP.NET Core response caching, cache invalidation

**Description:**  
Available slots for a given host + date don't change often. Cache the response for 60 seconds to reduce DB load.

**Warning:** Think carefully about cache key. The response must vary by `hostUserId`, `eventTypeId`, and `date`. A shared cache that ignores these would serve the wrong slots.

**Add:**
```csharp
[HttpGet("available-slots"), ResponseCache(Duration = 60, VaryByQueryKeys = ["hostUserId", "eventTypeId", "date"])]
```

**Plus register:** `builder.Services.AddResponseCaching()` and `app.UseResponseCaching()`.

**Definition of done:**
- [ ] Second identical request within 60s returns a cached response (verify via `X-Cache` header or response time)
- [ ] Different `hostUserId` gets its own cache entry
