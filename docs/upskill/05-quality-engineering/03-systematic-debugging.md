# 03 — Systematic Debugging

Five debugging scenarios you are likely to encounter in this codebase. Each scenario gives you a symptom, a hypothesis-driven investigation path, and the likely root cause.

---

## Scenario 1: POST /api/bookings returns 400 with no error body

**Symptom:** Integration test or manual request to `POST /api/bookings` returns `HTTP 400` but the response body is empty or `{}`.

**Initial hypotheses:**
1. `ValidationBehavior` threw `ValidationException` but `ExceptionHandlingMiddleware` didn't catch it correctly
2. Model binding failed (wrong Content-Type, malformed JSON)
3. The request body is null — `[FromBody]` silently failed

**Investigation path:**

Step 1 — Check Content-Type.
```
Content-Type: application/json   ← required
```
ASP.NET Core returns 400 silently if `Content-Type` is missing for `[FromBody]` parameters.

Step 2 — Check `ExceptionHandlingMiddleware`.
[`src/NetCalendy.Api/Middleware/ExceptionHandlingMiddleware.cs`](../../src/NetCalendy.Api/Middleware/ExceptionHandlingMiddleware.cs) — Does it catch `ValidationException`? Does it write a body? Look for `context.Response.WriteAsJsonAsync`.

Step 3 — Check Serilog logs. The middleware should log the exception before writing the 400. If nothing is in the logs, the exception happened before the middleware (e.g., model binding).

Step 4 — Enable detailed errors in dev: `appsettings.Development.json`:
```json
"DetailedErrors": true
```

**Likely root cause:** Missing `Content-Type: application/json` header. Second most common: JSON property names don't match the command record (case sensitivity — `StartTime` vs `startTime` works due to `JsonPropertyNameCaseInsensitive` in default options, but custom record property names can mismatch).

---

## Scenario 2: Domain events not firing — notification handler never runs

**Symptom:** You add an `INotificationHandler<BookingCreatedEvent>`, run a booking creation, and your handler is never called.

**Initial hypotheses:**
1. Handler not registered in DI
2. `AppDbContext.SaveChangesAsync` isn't finding the aggregate's events
3. The aggregate isn't in the EF change tracker

**Investigation path:**

Step 1 — Verify handler registration.
[`src/NetCalendy.Application/DependencyInjection.cs`](../../src/NetCalendy.Application/DependencyInjection.cs) — MediatR scans the Assembly. Is your handler in `NetCalendy.Application` assembly? If it's in a different project, that project's assembly must also be scanned.

Step 2 — Add a breakpoint (or log line) in `AppDbContext.SaveChangesAsync`.
[`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs:31`](../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs#L31)
Check: Is `domainEvents.Count > 0`? If zero, the aggregate's events aren't being found.

Step 3 — Check that the aggregate implements `IHasDomainEvents`.
[`src/NetCalendy.Domain/Common/IHasDomainEvents.cs`](../../src/NetCalendy.Domain/Common/IHasDomainEvents.cs)
`ChangeTracker.Entries<IHasDomainEvents>()` will only find entities that implement this interface. The `AggregateRoot<TId>` base class implements it — so any class extending `AggregateRoot` should work.

Step 4 — Check that `RaiseDomainEvent(...)` was actually called.
The aggregate's factory method (`Booking.Create`) must call `RaiseDomainEvent(new BookingCreatedEvent(...))`. Add a log line inside `RaiseDomainEvent` to confirm.

**Likely root cause:** Handler is in a project whose assembly isn't scanned by MediatR. Fix: add the assembly to the `AddMediatR` call.

---

## Scenario 3: 401 in integration tests — JWT validation fails

**Symptom:** Integration test sends a request with a `Bearer` token from `JwtTokenFactory`, but the API returns 401.

**Initial hypotheses:**
1. Token key mismatch — `JwtTokenFactory` uses a different key than the API configuration
2. Token expiry — test token generated with short expiry
3. Issuer/audience mismatch — JWT config requires specific issuer but token has none
4. Missing `Authorization` header — test forgot to add it

**Investigation path:**

Step 1 — Check `JwtTokenFactory` key.
[`tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs)
The key must match `appsettings.json` → `Jwt:Key`. If they diverge, JWT validation will fail silently (no error logged, just 401).

Step 2 — Check JWT configuration in API.
[`src/NetCalendy.Api/appsettings.json`](../../src/NetCalendy.Api/appsettings.json) and [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs)
Look for `ValidateIssuer` and `ValidateAudience`. If set to `true` with specific values, the test token must include matching `iss` and `aud` claims.

Step 3 — Decode the test token manually.
Paste the token at [jwt.io](https://jwt.io) (use offline tools for sensitive keys). Verify claims: `sub`, `tenant_id`, `tenant_slug`, `exp`.

Step 4 — Check that `WebApplicationFactory` uses the same `appsettings.json` key.
[`tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs)
The factory uses `ConfigureWebHost` — does it override the JWT key? If not, it uses the real `appsettings.json` key, which must match `JwtTokenFactory`.

**Likely root cause:** Key mismatch. Either `JwtTokenFactory` hardcodes a different key, or the test factory overrides app configuration in a way that changes the key.

---

## Scenario 4: Booking creates successfully but returns wrong tenant ID in response

**Symptom:** `POST /api/bookings` returns 201, but the returned booking's tenant in the database is from the wrong tenant (or worse, a different request's tenant).

**Initial hypotheses:**
1. `ICurrentTenantService` is scoped but resolves across requests incorrectly
2. `TenantResolutionMiddleware` is writing to the wrong `HttpContext`
3. Concurrent requests share a scoped service instance (DI lifetime bug)

**Investigation path:**

Step 1 — Verify `ICurrentTenantService` lifetime.
[`src/NetCalendy.Infrastructure/DependencyInjection.cs`](../../src/NetCalendy.Infrastructure/DependencyInjection.cs)
`CurrentTenantService` must be **Scoped**, not Singleton. A Singleton would share state across all requests — the first request's tenant would bleed into subsequent requests.

Step 2 — Trace the tenant resolution path.
[`src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs`](../../src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs)
The middleware stores `TenantId` in `HttpContext.Items["TenantId"]`. But `ICurrentTenantService.TenantId` reads from the **JWT claim** (`tenant_id`), not from `Items`. Confirm which one the handler uses.

Step 3 — Check `CreateBookingHandler`.
[`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)
`var tenantId = TenantId.From(currentTenant.TenantId!.Value);` — if `currentTenant.TenantId` is null, this throws. If it's the wrong value, check the JWT claims in the test token.

**Likely root cause:** `ICurrentTenantService` registered as Singleton instead of Scoped, or JWT `tenant_id` claim doesn't match the `X-Tenant-Slug` header value (they resolve independently).

---

## Scenario 5: EF Core migration fails — "column does not exist" in production

**Symptom:** After deploying, the API starts returning 500 errors. Logs show `42703: column "..." does not exist` from PostgreSQL.

**Initial hypotheses:**
1. EF migration wasn't applied before the app started
2. Migration was applied against the wrong database
3. EF model doesn't match the applied migration (code changed after migration was generated)

**Investigation path:**

Step 1 — Check migration history.
Connect to the database and run:
```sql
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```
Compare the last applied migration against the migration files in `src/NetCalendy.Infrastructure/Persistence/Migrations/`.

Step 2 — Check the EF model vs. schema.
Run `dotnet ef migrations list` to see which migrations are pending. If there are pending migrations, the app deployed without running them.

Step 3 — Check automatic migration on startup.
[`src/NetCalendy.Infrastructure/DependencyInjection.cs`](../../src/NetCalendy.Infrastructure/DependencyInjection.cs) or `Program.cs` — does the app call `db.Database.MigrateAsync()` on startup? If it does, a startup failure (container crash before migration completes) would leave the DB in an intermediate state.

Step 4 — Look at the specific column mentioned.
The column name (e.g., `Email_Value` instead of `Email`) tells you which EF owned type or value converter mapping is wrong. Check [`src/NetCalendy.Infrastructure/Persistence/Configurations/`](../../src/NetCalendy.Infrastructure/Persistence/Configurations/) for the entity's configuration.

**Likely root cause:** Code was deployed without running the corresponding migration. Fix: run `dotnet ef database update` before starting the app, or use an explicit migration runner as part of the deployment pipeline.

**Prevention:** Add a health check that verifies the DB schema matches the expected migration hash at startup.
