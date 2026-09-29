# 00 — Fast Track (Weekend Win)

**Goal:** Boot the project, call two endpoints, trace one request end-to-end, make one safe change, and explain the architecture out loud. Skips everything except what you need to prove the thing works and understand its skeleton.

---

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 9.0+ | `dotnet --version` |
| Node.js | 20+ | `node --version` |
| Docker Desktop | any | For Postgres + Seq |

---

## 2. Boot the Infrastructure (⚠️ inferred — not CI-verified)

```bash
# From repo root
docker compose -f docker-compose.dev.yml up -d
# Starts: postgres:5432, seq:5341
```

Verify Postgres is ready:
```bash
docker compose -f docker-compose.dev.yml ps
# postgres container should say "healthy"
```

---

## 3. Generate and Apply the First EF Migration (inferred)

```bash
dotnet ef migrations add InitialCreate \
  --project src/NetCalendy.Infrastructure \
  --startup-project src/NetCalendy.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/NetCalendy.Infrastructure \
  --startup-project src/NetCalendy.Api
```

> **What just happened?** EF Core read all `IEntityTypeConfiguration<T>` files under `src/NetCalendy.Infrastructure/Persistence/Configurations/` and generated a SQL schema migration. The database now has tables: `tenants`, `users`, `event_types`, `schedules`, `schedule_rules`, `bookings`.

---

## 4. Run the API

```bash
dotnet run --project src/NetCalendy.Api
# Default: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

---

## 5. Run the Frontend

```bash
cd web
npm install
npm run dev
# http://localhost:5173  — proxies /api/* to :5000
```

---

## 6. First API Call: Create a Tenant

The tenant is the top-level isolation unit. Every user, event type, and booking belongs to a tenant.

```bash
curl -X POST http://localhost:5000/api/tenants \
  -H "Content-Type: application/json" \
  -d '{"name": "Acme Corp", "slug": "acme"}'
# Expected: 201 Created, body contains tenantId
```

Open [`src/NetCalendy.Api/Controllers/TenantsController.cs`](../../src/NetCalendy.Api/Controllers/TenantsController.cs) — it's 13 lines. The controller calls `mediator.Send(command)`. That's it. The controller owns HTTP shape; the handler owns logic.

---

## 7. Your First 10 Files, In Order

Read these before anything else. The "why" column tells you what mental model to build.

| # | File | Why |
|---|---|---|
| 1 | [`src/NetCalendy.Domain/Common/Result.cs`](../../src/NetCalendy.Domain/Common/Result.cs) | Every handler returns `Result<T>`. Understand this first or nothing else makes sense. |
| 2 | [`src/NetCalendy.Domain/Common/AggregateRoot.cs`](../../src/NetCalendy.Domain/Common/AggregateRoot.cs) | Base for all business entities. Holds domain events. |
| 3 | [`src/NetCalendy.Domain/Bookings/Booking.cs`](../../src/NetCalendy.Domain/Bookings/Booking.cs) | The most complex aggregate. Shows factory method, invariant enforcement, domain events. |
| 4 | [`src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs`](../../src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs) | Every command is validated here before reaching a handler. |
| 5 | [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs) | Shows a full command handler: load, validate business rules, create aggregate, persist, return. |
| 6 | [`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs`](../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs) | Implements `IUnitOfWork`. Dispatches domain events after `SaveChanges`. |
| 7 | [`src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs`](../../src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs) | Shows how the domain model maps to columns — owned types, value object converters, indexes. |
| 8 | [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs) | Wires everything: DI, JWT, Serilog, CORS, middleware order. |
| 9 | [`src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs`](../../src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs) | Multi-tenancy entry point. Runs before auth. |
| 10 | [`tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs`](../../tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs) | Shows how to test domain logic with zero infrastructure. |

---

## 8. Trace One Request: Book a Slot

Follow this flow without running the code first. Use the file list above.

1. `POST /api/bookings` hits [`BookingsController.cs:19`](../../src/NetCalendy.Api/Controllers/BookingsController.cs#L19)
2. Controller calls `mediator.Send(command)` — no logic here
3. MediatR pipeline: `LoggingBehavior` → `ValidationBehavior` (runs `CreateBookingValidator`) → `CreateBookingHandler`
4. Handler loads `EventType` by ID — if not found, returns `Result.Failure(Error.NotFound(...))`
5. Handler calls `bookings.GetConflictingBookingsAsync` — if overlap, returns `Error.Conflict(...)`
6. Handler calls `Invitee.Create(...)` — validates name/email/timezone as value object
7. Handler calls `Booking.Create(...)` — enforces start-in-future invariant, raises `BookingCreatedEvent`
8. `bookings.Add(booking)` registers the entity with EF Core change tracker
9. `uow.SaveChangesAsync()` hits `AppDbContext.SaveChangesAsync` — persists, then dispatches domain events
10. Handler returns `Result.Success(new CreateBookingResponse(...))`
11. `ResultExtensions.ToCreatedResult` maps to HTTP 201

🔧 **Drill:** On paper, write the data shape at each step. What's in memory after step 4? After step 7? After step 9?

---

## 9. One Safe Change: Add a Field to the Booking Response

**Change:** Add `durationMinutes` to `CreateBookingResponse` so the caller knows how long the booking is.

Files you'll need to touch:
- [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingCommand.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingCommand.cs) — add field to the response record
- [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs) — populate it (you already have `eventType.Duration.Minutes` in scope)

No domain changes. No infrastructure changes. No migrations. This is why Clean Architecture gives you safe refactoring zones.

**Verify:** After the change, write a test asserting `result.Value.DurationMinutes == 30`.

---

## 10. Run the Tests

```bash
# Domain unit tests — no infrastructure needed
dotnet test tests/NetCalendy.Domain.Tests

# Application unit tests — no infrastructure needed
dotnet test tests/NetCalendy.Application.Tests

# Integration tests — requires Docker (Testcontainers starts a real Postgres)
dotnet test tests/NetCalendy.Integration.Tests
```

---

## 11. Teach-Back Check

Say out loud (or write) the answer to each question. If you can't, read the linked file.

1. What does `Result<T>` replace that most code uses instead? *(hint: exceptions)*
2. Why does the controller have no `if` statements? *(hint: handler's job)*
3. What is a "tenant" in this system and where is it enforced? *(hint: `TenantId` column + JWT claim)*
4. Why does `Booking.Create` use a static factory method rather than `new Booking(...)`? *(hint: invariants)*
5. What happens if you call `SaveChangesAsync` and the database is down? Does the domain event still fire? *(hint: `AppDbContext.SaveChangesAsync` line 44)*

---

## What This Skips

- EF migrations internals
- JWT token generation / auth server setup
- All React flows
- Observability (Serilog/Seq)
- Docker multi-stage build
- Performance, security, deployment

Those live in `02-stack-and-language-mastery/` and `05-quality-engineering/`.
