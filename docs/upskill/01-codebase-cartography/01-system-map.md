# 01 — System Map

## Solution Graph

```
NetCalendy.sln
├── src/
│   ├── NetCalendy.Domain          ← No dependencies except MediatR.Contracts
│   ├── NetCalendy.Application     ← Depends on: Domain
│   ├── NetCalendy.Infrastructure  ← Depends on: Application (and transitively Domain)
│   └── NetCalendy.Api             ← Depends on: Application + Infrastructure
└── tests/
    ├── NetCalendy.Domain.Tests    ← Depends on: Domain
    ├── NetCalendy.Application.Tests ← Depends on: Application + Domain
    └── NetCalendy.Integration.Tests ← Depends on: Api + Application + Domain
```

The dependency arrow always points **inward**. Domain never knows about EF Core. Application never knows about HTTP. This is the Clean Architecture invariant.

## Project Ownership Table

| Project | Owns | Must NOT own |
|---|---|---|
| `Domain` | Aggregates, value objects, domain events, repository *interfaces*, `Result<T>` | HTTP, EF Core, MediatR handlers, DI, logging |
| `Application` | Commands, queries, MediatR handlers, FluentValidation validators, pipeline behaviors | EF Core, HTTP, database connections, `HttpContext` |
| `Infrastructure` | `AppDbContext`, EF configurations, repository *implementations*, `CurrentTenantService`, `CurrentUserService` | ASP.NET route logic, business invariants |
| `Api` | Controllers, middleware, `Program.cs`, response mapping (`ResultExtensions`) | Business logic, database queries |
| `*.Tests` | Test classes, test fixtures, fakes/mocks | Production logic |

## Runtime Processes

| Process | Entry point | Port | Notes |
|---|---|---|---|
| API server | `src/NetCalendy.Api/Program.cs` | 5000 | Kestrel; serves `/api/*` |
| React dev | `web/src/main.tsx` | 5173 | Vite dev server; proxies `/api/*` to 5000 |
| React prod | `web/Dockerfile` (nginx) | 80 | Serves static + proxies `/api/*` to api container |
| PostgreSQL | `docker-compose.dev.yml` | 5432 | Shared between dev and tests |
| Seq | `docker-compose.dev.yml` | 5341 | Structured log UI |

## What Lives Where

### Domain Layer
```
src/NetCalendy.Domain/
├── Common/           # Entity, AggregateRoot, ValueObject, Result, Error, IDomainEvent, IHasDomainEvents, DomainEvent
├── Tenants/          # Tenant aggregate + TenantId + TenantCreatedEvent
├── Users/            # User aggregate + UserId + Email (VO) + UserRole + UserRegisteredEvent
├── EventTypes/       # EventType aggregate + EventTypeId + Duration (VO) + EventTypeCreatedEvent
├── Schedules/        # Schedule aggregate + ScheduleId + AvailabilityRule (VO) + TimeRange (VO) + TimeSlot
├── Bookings/         # Booking aggregate + BookingId + BookingStatus + Invitee (VO) + events
└── Repositories/     # IRepository<T,TId> + 5 specific interfaces
```

### Application Layer
```
src/NetCalendy.Application/
├── Common/
│   ├── Behaviors/    # LoggingBehavior, ValidationBehavior (MediatR pipeline)
│   ├── IUnitOfWork.cs
│   ├── ICurrentUserService.cs
│   ├── ICurrentTenantService.cs
│   └── IDateTimeProvider.cs
├── Tenants/          # CreateTenant command
├── Users/            # RegisterUser command
├── EventTypes/       # Create/Update/Delete commands + Get/List queries
├── Schedules/        # CreateSchedule command + GetAvailableSlots query
├── Bookings/         # Create/Cancel commands + Get/List queries
└── DependencyInjection.cs
```

### Infrastructure Layer
```
src/NetCalendy.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/   # 5 IEntityTypeConfiguration<T> classes
│   ├── Repositories/     # 5 repository implementations + RepositoryBase
│   └── Migrations/       # Generated EF migrations (empty until first `dotnet ef migrations add`)
├── Services/             # CurrentTenantService, CurrentUserService, DateTimeProvider
└── DependencyInjection.cs
```

### API Layer
```
src/NetCalendy.Api/
├── Controllers/          # TenantsController, EventTypesController, SchedulesController, BookingsController
├── Extensions/           # ResultExtensions (maps Result<T> to IActionResult)
├── Middleware/           # TenantResolutionMiddleware, ExceptionHandlingMiddleware
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

### React Frontend
```
web/src/
├── api/          # client.ts (axios), eventTypes.ts, bookings.ts
├── components/
│   ├── booking/  # BookingPage.tsx (3-step slot picker → form → confirmed)
│   └── dashboard/ # EventTypeCard.tsx
├── hooks/        # useEventTypes.ts, useAvailableSlots.ts
├── lib/          # utils.ts (cn, formatDateTime, slugify)
├── pages/        # DashboardPage.tsx, BookPage.tsx
└── types/        # index.ts (EventType, AvailableSlot, Booking, CreateBookingPayload)
```

## Architectural Boundaries (Visual)

```
Browser ──────► React (web/)
                    │ HTTP /api/*
                    ▼
              ExceptionHandlingMiddleware
              TenantResolutionMiddleware
              [Authentication / Authorization]
                    │
              Controllers (Api/)
                    │ mediator.Send()
                    ▼
              MediatR Pipeline:
              LoggingBehavior → ValidationBehavior → Handler (Application/)
                    │
              Handler calls:
              ├─ Repository interfaces (Domain/) ← implemented by Infrastructure/
              ├─ Aggregate factory methods (Domain/)
              └─ IUnitOfWork.SaveChangesAsync()
                    │
              AppDbContext (Infrastructure/)
              ├─ EF Core → PostgreSQL
              └─ IPublisher.Publish(domainEvent) [after save]
```

**The arrow of knowledge:** Application layer knows about Domain. Infrastructure knows about Application (to implement its interfaces). API knows about Application (to send commands). Neither the Domain nor the Application knows how data is stored or how HTTP works.
