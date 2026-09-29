# 05 — Security Checklist

Tied to real code in this repo. For each item: find the relevant file, understand the current state, assess the risk, and know how to fix it.

---

## 1. Authorization: Every Endpoint Has Correct Auth Level

| Endpoint | Auth Level | Correct? |
|---|---|---|
| `POST /api/bookings` | `[Authorize]` | **Yes** — but should some booking creation be public? (RISK-005) |
| `GET /api/bookings/{id}` | `[Authorize]` | Yes |
| `POST /api/bookings/cancel` | `[AllowAnonymous]` | Yes — cancellation by token, no login needed |
| `GET /api/event-types` | `[Authorize]` | Yes — host views their own event types |
| `GET /api/event-types/{id}` | `[Authorize]` | **Potentially wrong** — public booking flow needs to view event types |
| `GET /api/schedules/available-slots` | Check file | Verify |

**How to verify:** Read [`src/NetCalendy.Api/Controllers/`](../../src/NetCalendy.Api/Controllers/) — every `[HttpGet]`, `[HttpPost]`, etc. should have either `[Authorize]` (inherited from class) or an explicit `[AllowAnonymous]` if public access is intended.

**Rule for review:** If you can't explain *why* an endpoint is public, it should require auth.

---

## 2. IDOR: Tenant Scope on All Get-By-ID Calls

**Current risk (RISK-001):** [`src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs) — `GetByIdAsync` queries by ID only, no tenant filter.

**Proof of concept:**
- Tenant A creates a booking with `bookingId = abc-123`
- Tenant B's authenticated user requests `GET /api/bookings/abc-123`
- The handler loads the booking — no tenant check — and returns Tenant A's data

**Fix pattern:**
```csharp
// After loading any entity by ID, add:
if (entity.TenantId != TenantId.From(currentTenant.TenantId!.Value))
    return Result.Failure<T>(Error.NotFound(entityType, id)); // 404, not 403 — don't confirm existence
```

**Files to fix:** Every `GetBookingHandler`, `GetEventTypeHandler`, `GetScheduleHandler`.

**Checklist item:** For every handler that loads an entity by ID, ask: "Does this check tenant scope?"

---

## 3. Broken Object Level Authorization: Ownership on Updates

**Current state:** `UpdateEventTypeHandler` and `DeleteEventTypeHandler` check that `et.OwnerId == currentUser.UserId` before allowing changes. See [`src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeHandler.cs`](../../src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeHandler.cs).

**Risk:** Any new update/delete handler that forgets this check. It's a per-handler convention, not enforced by the framework.

**Checklist item:** Every update/delete handler must:
1. Load the entity
2. Check tenant scope (IDOR fix above)
3. Check ownership (`entity.OwnerId == currentUserId`)
4. Return `Error.Validation("X.Forbidden", "...")` or `Error.NotFound(...)` on mismatch

**Why `NotFound` instead of `Forbidden`?** Returning 403 confirms to an attacker that the resource exists. Returning 404 prevents enumeration.

---

## 4. Input Validation: No Trust of User-Supplied Strings

**Timezone strings (RISK-002):** [`src/NetCalendy.Domain/Users/Invitee.cs`](../../src/NetCalendy.Domain/Users/Invitee.cs) stores timezone as a string. `TimeZoneInfo.FindSystemTimeZoneById(timezone)` throws `TimeZoneNotFoundException` on Windows for IANA names ("America/New_York") and on Linux for Windows names ("Eastern Standard Time").

**Fix:**
```csharp
// Validation in CreateBookingValidator
RuleFor(c => c.InviteeTimezone)
    .Must(tz => TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
    .WithMessage("Timezone is not recognized on this system.");
```

Or use the `TimeZoneConverter` NuGet package for cross-platform IANA↔Windows mapping.

**SQL injection:** Not a risk — EF Core uses parameterized queries. Never use `FromSqlRaw(string.Format(...))` with user input.

**XSS:** The API returns JSON, not HTML. XSS is the frontend's concern. In the React app, avoid `dangerouslySetInnerHTML` with user-provided data.

---

## 5. JWT Security

**Current state:**
- Key in `appsettings.json`: `"REPLACE_WITH_A_SECRET_KEY_AT_LEAST_32_CHARS"` — placeholder that must be overridden
- No `ValidateIssuer` or `ValidateAudience` in current config (see [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs))

**Risks:**

| Risk | Severity | Fix |
|---|---|---|
| Placeholder key in appsettings.json committed to git | Critical | Use .NET User Secrets in dev, env var in prod |
| No issuer/audience validation | Medium | Add `ValidIssuer` and `ValidAudience` in JWT config |
| Token doesn't expire (no `exp` claim check) | Actually: EF Core validates `exp` by default | Verified safe |
| `!.Value` on nullable claim (e.g., `User.FindFirst("sub")!.Value`) | Medium | `NullReferenceException` if claim missing; use null-conditional |

**Never commit a real JWT key.** Use:
```bash
# Development
dotnet user-secrets set "Jwt:Key" "your-actual-secret-key"

# Production (environment variable)
export Jwt__Key="your-actual-secret-key"
```

---

## 6. Rate Limiting

**Current state (RISK-006):** No rate limiting on any endpoint.

**Attack surface:**
- `POST /api/bookings` — flooding this creates bookings for a host (DoS for the host's schedule)
- `POST /api/bookings/cancel` — trying random cancellation tokens (brute force)
- `GET /api/schedules/available-slots` — scraping all hosts' availability

**ASP.NET Core 8 fix (built-in):**
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("PublicApi", o =>
    {
        o.PermitLimit = 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 2;
    });
});

// Endpoint or controller attribute:
[EnableRateLimiting("PublicApi")]
```

---

## 7. Sensitive Data in Logs

**Current state:** Serilog is configured to structured-log requests. If request bodies are logged (not the default), invitee email addresses would appear in logs.

**Check:** [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs) — does Serilog log request bodies?

**Rule:** Never log PII (emails, names, phone numbers) at Info level. Use `{Email:l}` destructuring carefully, or scrub before logging.

**What is safe to log:** Booking IDs, tenant IDs (opaque GUIDs), request method, path, status code, elapsed time.

---

## Security Pre-Merge Checklist

- [ ] New `GET /api/.../{id}` endpoint: handler checks tenant scope after loading
- [ ] New `PUT/DELETE` endpoint: handler checks ownership
- [ ] New public endpoint: explicit `[AllowAnonymous]` with documented reason
- [ ] New string field accepted from user input: validated in FluentValidation
- [ ] New configuration secret: uses User Secrets/env var, not appsettings.json
- [ ] No `dangerouslySetInnerHTML` with user data in React components
- [ ] No raw SQL with string interpolation
