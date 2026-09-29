# 06 — Observability and Operations

## What Observability Means

"I don't need to SSH into the server to understand what my system is doing."

The three pillars:
- **Logs:** What happened and when (per-request narrative)
- **Metrics:** How the system is behaving in aggregate (request rates, error rates, latency)
- **Traces:** The path of a single request through multiple services

This repo implements logs (Serilog). Metrics and traces are not yet implemented.

---

## Serilog: Current Setup

**Configuration:** [`src/NetCalendy.Api/appsettings.json`](../../src/NetCalendy.Api/appsettings.json) and [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs)

**Sinks configured:**
- Console (structured JSON in production, pretty-print in development)
- Seq (structured log aggregation — `http://localhost:5341` by default)

**Enrichers:** `FromLogContext` — allows structured properties to be pushed onto any log statement in scope.

**What is logged automatically:**
- Every HTTP request/response via Serilog's request logging middleware
- All application warnings and errors via the logging pipeline behavior

**What `LoggingBehavior` logs:**
```
Information: Handling {RequestName}
Information: Handled {RequestName} in {ElapsedMs}ms
Warning: (on slow requests — add this)
```
File: [`src/NetCalendy.Application/Common/Behaviors/LoggingBehavior.cs`](../../src/NetCalendy.Application/Common/Behaviors/LoggingBehavior.cs)

---

## Reading Seq

Seq is a structured log viewer. Start it locally:
```bash
docker run --rm -e ACCEPT_EULA=Y -p 5341:80 datalust/seq
```
Then navigate to `http://localhost:5341`.

**Useful queries in Seq:**

Find all failed requests:
```
@Level = 'Error'
```

Find slow MediatR handlers:
```
ElapsedMs > 500
```

Find all bookings for a specific tenant:
```
TenantId = '00000000-0000-0000-0000-000000000001'
```

Find all 409 conflicts:
```
StatusCode = 409
```

---

## Adding Structured Context to Logs

Serilog's `LogContext.PushProperty` adds properties to all logs emitted within a scope:

```csharp
// In LoggingBehavior.cs — add TenantId and UserId to every log in the request:
using (LogContext.PushProperty("TenantId", currentTenant.TenantId))
using (LogContext.PushProperty("UserId", currentUser.UserId))
{
    return await next();
}
```

This makes every log line within a request searchable by tenant and user — essential for debugging multi-tenant issues.

---

## Health Checks

**Current state:** No health checks implemented.

**What to add:**

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "postgres", tags: ["db", "ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

app.MapHealthChecks("/health/live", new() { Predicate = hc => hc.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = hc => hc.Tags.Contains("ready") });
```

**Why two endpoints?**
- `/health/live` — Is the process running? (Kubernetes liveness probe)
- `/health/ready` — Can the app serve traffic? Checks DB connection. (Kubernetes readiness probe)

---

## Deploy and Rollback

**Current state:** No deployment pipeline in this repo.

**Expected deployment flow for a .NET API + React SPA:**

```
1. CI runs tests (dotnet test + vitest)
2. CI builds Docker image
3. Image pushed to container registry
4. Deployment:
   a. Run DB migrations: dotnet ef database update
   b. Deploy new container (rolling update or blue/green)
5. Health check: /health/ready must return 200 before routing traffic
```

**Rollback checklist:**
1. Was a migration applied? Is it reversible?
   - EF Core migrations have a `Down()` method — run `dotnet ef database update <PreviousMigration>`
   - Some migrations can't be rolled back (drop column, NOT NULL addition with no default)
2. Redeploy previous container image
3. Verify health checks pass

**Zero-downtime migrations:**
- Never add a NOT NULL column without a default in a single deploy
- Pattern: Add nullable → deploy → backfill → add NOT NULL constraint → deploy
- Never rename a column in one step — add new column, backfill, update code, remove old column

---

## Alerting (What to Set Up)

When this goes to production, the minimum alert set:

| Alert | Threshold | Severity |
|---|---|---|
| Error rate > 1% | 5-minute window | Page |
| P95 response time > 2s | 5-minute window | Page |
| DB connection failures | Any | Page |
| Disk space > 80% | | Warning |
| JWT key age > 90 days | | Warning |

These would be implemented in the monitoring platform (Grafana + Prometheus, Datadog, etc.) — not in the application code.

---

## Correlating a Production Bug

When a user reports "my booking failed," here's the investigation path:

1. **Get the approximate time** from the user
2. **Find the request in Seq/logs** by time window and endpoint: `@Level = 'Error' AND RequestPath = '/api/bookings'`
3. **Grab the correlation ID** if you have one (add `CorrelationId` middleware to generate and log a request ID)
4. **Find all log lines** for that correlation ID — trace the full request path
5. **Check the error** — was it a domain validation failure, a DB error, or an unhandled exception?
6. **Reproduce locally** with the same input parameters

**Missing today:** No `CorrelationId` middleware. Add it for production:
```csharp
// illustrative
app.Use(async (ctx, next) => {
    var id = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    using (LogContext.PushProperty("CorrelationId", id)) {
        ctx.Response.Headers["X-Correlation-Id"] = id;
        await next();
    }
});
```
