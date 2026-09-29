# 03 — Validation, Auth, and Permissions

## Three Validation Layers

| Layer | File | What it checks | What it doesn't check |
|---|---|---|---|
| HTTP input | `CreateBookingValidator.cs` | Format: email valid, startTime future, name non-empty | Business rules: is the slot available? |
| Domain invariants | `Booking.Create()` | Business rules: start < end, not in past | Format already handled |
| Database constraints | EF config + Postgres | Unique cancellation token, FK constraints | Logic |

The layers are intentionally overlapping — the domain doesn't trust that the validator ran. The validator doesn't trust that domain rules match. Defense-in-depth.

---

## Authentication Flow

1. JWT Bearer token in `Authorization: Bearer <token>` header
2. ASP.NET Core `JwtBearerHandler` validates: signature, issuer, audience, expiry
3. On success: `HttpContext.User` populated with claims
4. `[Authorize]` attribute on controller/action requires an authenticated user

**What the JWT must contain:**
- `sub` (or `NameIdentifier`) — user ID (GUID string)
- `tenant_id` — tenant ID (GUID string)
- `tenant_slug` — tenant slug string

**No auth server in this repo.** There's no login endpoint, no token generation. You need an external identity provider (Auth0, Keycloak, custom) that issues tokens with the right claims.

**Test tokens:** `JwtTokenFactory.GenerateToken(userId, tenantId)` at [`tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs) — uses the same key as `appsettings.json`.

---

## Authorization Architecture

**Coarse-grained:** `[Authorize]` on controller classes. `[AllowAnonymous]` on specific actions.

**Fine-grained (missing):** No ASP.NET Core `IAuthorizationHandler` / policies exist. Resource-level authorization (e.g., "can this user modify this event type?") is done inline in handlers:

```csharp
// UpdateEventTypeHandler.cs:15
if (et.OwnerId != UserId.From(currentUser.UserId!.Value))
    return Result.Failure(Error.Validation("EventType.Forbidden", "You do not own this event type."));
```

This is correct behavior, but it's scattered across handlers rather than being in a centralized authorization policy.

---

## Multi-Tenancy Enforcement

**How tenant is resolved:**
1. `TenantResolutionMiddleware` runs before auth — reads `X-Tenant-Slug` header or subdomain
2. Looks up `Tenant` by slug in the database
3. Stores `TenantId` in `HttpContext.Items`
4. **Note:** This doesn't set a claim — it only sets `Items`. The JWT claim `tenant_id` is separate.

**How tenant is used in handlers:**
1. `ICurrentTenantService.TenantId` reads the `tenant_id` JWT claim (not `HttpContext.Items`)
2. Handlers pass `tenantId` to repository calls

**The gap:** `TenantResolutionMiddleware` validates that the slug exists and stores the ID in `Items`, but handlers read from the JWT claim. If the JWT `tenant_id` doesn't match the resolved tenant from the header, the middleware and handler are using different tenant contexts. **This is a potential IDOR vector — investigate whether these are kept in sync.**

---

## IDOR Risk and the GetById Gap

**Definition:** Insecure Direct Object Reference — accessing a resource by ID without verifying the caller is authorized to see it.

**Current state:** `RepositoryBase.GetByIdAsync(id)` at [`RepositoryBase.cs:9`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs#L9) takes only an ID. No tenant check. No ownership check.

**Vulnerable endpoints:**
- `GET /api/bookings/{id}` — `GetBookingHandler` loads by ID, no tenant scope
- `GET /api/event-types/{id}` — `GetEventTypeHandler` loads by ID, no tenant scope

**Correct pattern to add:**
```csharp
// Illustrative fake code: not from this repo
var booking = await bookings.GetByIdAsync(BookingId.From(query.BookingId), ct);
if (booking is null) return Result.Failure<BookingDto>(Error.NotFound("Booking", query.BookingId));

// Add this:
if (booking.TenantId != TenantId.From(currentTenant.TenantId!.Value))
    return Result.Failure<BookingDto>(Error.NotFound("Booking", query.BookingId)); // Return 404, not 403 (don't confirm existence)
```

---

## Junior Misses vs Senior Checks

### Junior mistakes in code review:
- Approves code that adds a new GET endpoint without checking if the response needs tenant filtering
- Doesn't notice that `[AllowAnonymous]` was accidentally removed from a public endpoint
- Thinks FluentValidation is "the" authorization layer

### Senior checks:
- Every new "get by ID" endpoint: does it check tenant and ownership?
- Every `[AllowAnonymous]` endpoint: what's the blast radius if this data is publicly enumerable?
- JWTs have expiry — what happens when a token expires mid-session? Does the 401 interceptor handle it gracefully?
- Tenant and user ID in JWT: who issues these? Are they mutable? Can a user change tenants?

---

## Permissions Pre-Merge Checklist

- [ ] New endpoint: is `[Authorize]` or `[AllowAnonymous]` correct and intentional?
- [ ] New "get by ID": does the handler verify tenant scope after loading?
- [ ] New "update/delete": does the handler verify ownership?
- [ ] New public endpoint: has it been analyzed for enumeration/scraping risk?
- [ ] New claim usage: is the claim validated to be present before `!.Value`?
