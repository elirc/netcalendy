# NetCalendy

A production-grade Calendly-style booking SaaS built with C# / .NET 9 and React.

## Architecture

```
src/
  NetCalendy.Domain/        # Aggregates, Value Objects, Domain Events, Repository interfaces
  NetCalendy.Application/   # CQRS commands & queries (MediatR), validators (FluentValidation)
  NetCalendy.Infrastructure/ # EF Core + PostgreSQL, repository implementations, services
  NetCalendy.Api/           # ASP.NET Core controllers, JWT auth, middleware

tests/
  NetCalendy.Domain.Tests/        # Pure domain unit tests (xUnit + FluentAssertions)
  NetCalendy.Application.Tests/   # Handler unit tests (NSubstitute mocks)
  NetCalendy.Integration.Tests/   # End-to-end API tests (WebApplicationFactory + Testcontainers)

web/                        # React + TypeScript + Vite frontend
```

### Key design decisions

| Concern | Choice | Why |
|---|---|---|
| Architecture | Clean Architecture / DDD | Domain is dependency-free; infra depends inward |
| CQRS | MediatR | Decoupled handlers, pipeline behaviors for cross-cutting concerns |
| Multi-tenancy | Tenant ID per row | Simple, performant, no schema management overhead |
| Validation | FluentValidation pipeline behavior | Validated before handlers execute; clean error surface |
| Domain events | `IHasDomainEvents` + MediatR `IPublisher` | Events dispatched after `SaveChanges` succeeds |
| Auth | JWT Bearer | Stateless; tenant + user claims in token |
| Integration tests | Testcontainers (real Postgres) | No mock/real divergence risk |

## Prerequisites

- [.NET 9 SDK](https://dot.net)
- [Node.js 20+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Quick start (development)

```bash
# 1. Start Postgres + Seq
docker compose -f docker-compose.dev.yml up -d

# 2. Apply EF migrations
dotnet ef migrations add InitialCreate \
  --project src/NetCalendy.Infrastructure \
  --startup-project src/NetCalendy.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/NetCalendy.Infrastructure \
  --startup-project src/NetCalendy.Api

# 3. Run the API
dotnet run --project src/NetCalendy.Api

# 4. Run the React frontend
cd web && npm install && npm run dev
```

API is at `http://localhost:5000` · Swagger at `http://localhost:5000/swagger`  
Frontend at `http://localhost:5173` · Seq logs at `http://localhost:5341`

## Running tests

```bash
# Domain + Application unit tests (no Docker needed)
dotnet test tests/NetCalendy.Domain.Tests
dotnet test tests/NetCalendy.Application.Tests

# Integration tests (Docker required for Testcontainers)
dotnet test tests/NetCalendy.Integration.Tests
```

## Production deployment

```bash
cp .env.example .env        # set JWT_KEY
docker compose up -d
```

## API reference

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/tenants` | None | Create a new tenant/organization |
| `POST` | `/api/event-types` | Bearer | Create event type |
| `GET` | `/api/event-types` | Bearer | List your event types |
| `PUT` | `/api/event-types/:id` | Bearer | Update event type |
| `DELETE` | `/api/event-types/:id` | Bearer | Deactivate event type |
| `POST` | `/api/schedules` | Bearer | Create availability schedule |
| `GET` | `/api/schedules/available-slots` | None | Get open booking slots |
| `POST` | `/api/bookings` | None | Create a booking (invitee) |
| `GET` | `/api/bookings` | Bearer | List bookings (host) |
| `GET` | `/api/bookings/:id` | Bearer | Get booking detail |
| `POST` | `/api/bookings/cancel` | None | Cancel a booking (token-based) |

## Tenant resolution

Pass the tenant in one of two ways:
1. **Header**: `X-Tenant-Slug: your-org`
2. **Subdomain**: `your-org.yourdomain.com`

JWT tokens must include `tenant_id` and `tenant_slug` claims.
