# 02 — File Reading Order

Read these files in sequence. The "What to look for" column tells you the one thing to extract from each file. Don't rabbit-hole into understanding every line — build the map first.

---

## Junior Track (Files 1–18)

| # | File | Why now | What to look for |
|---|---|---|---|
| 1 | [`src/NetCalendy.Domain/Common/Result.cs`](../../src/NetCalendy.Domain/Common/Result.cs) | Every handler return type | How `Success<T>` and `Failure` are constructed; when `Value` throws |
| 2 | [`src/NetCalendy.Domain/Common/Error.cs`](../../src/NetCalendy.Domain/Common/Error.cs) | Companion to Result | The four factory methods; what "Code" means |
| 3 | [`src/NetCalendy.Domain/Common/Entity.cs`](../../src/NetCalendy.Domain/Common/Entity.cs) | Base class | How equality is by ID, not reference |
| 4 | [`src/NetCalendy.Domain/Common/AggregateRoot.cs`](../../src/NetCalendy.Domain/Common/AggregateRoot.cs) | Domain event plumbing | `RaiseDomainEvent` and `ClearDomainEvents` |
| 5 | [`src/NetCalendy.Domain/Common/ValueObject.cs`](../../src/NetCalendy.Domain/Common/ValueObject.cs) | Equality-by-content | `GetEqualityComponents` pattern |
| 6 | [`src/NetCalendy.Domain/Tenants/Tenant.cs`](../../src/NetCalendy.Domain/Tenants/Tenant.cs) | Simplest aggregate | Factory method, validation, domain event — 3 lines each |
| 7 | [`src/NetCalendy.Domain/Bookings/Booking.cs`](../../src/NetCalendy.Domain/Bookings/Booking.cs) | Most complex aggregate | Constructor is private, `Create` is the only entry point, `Cancel` state machine |
| 8 | [`src/NetCalendy.Domain/Schedules/Schedule.cs`](../../src/NetCalendy.Domain/Schedules/Schedule.cs) | Aggregate with business logic | `GetAvailableSlots` loop |
| 9 | [`src/NetCalendy.Application/Common/IUnitOfWork.cs`](../../src/NetCalendy.Application/Common/IUnitOfWork.cs) | Interface contract | One method — why abstract it? |
| 10 | [`src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs`](../../src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs) | Cross-cutting validation | How it runs all validators and throws |
| 11 | [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingCommand.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingCommand.cs) | Command/Response shape | A `record` that is the input; another `record` that is the output |
| 12 | [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs) | FluentValidation | `RuleFor` chains; `When` predicate |
| 13 | [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs) | Handler pattern | Dependency injection via primary constructor; early-return Result pattern |
| 14 | [`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs`](../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs) | DbContext + UoW | How `SaveChangesAsync` collects and dispatches domain events |
| 15 | [`src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs`](../../src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs) | EF config | `OwnsOne(b => b.Invitee, ...)` maps value object to columns; value converters for ID types |
| 16 | [`src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs) | Repository impl | LINQ queries; no business logic |
| 17 | [`src/NetCalendy.Api/Controllers/BookingsController.cs`](../../src/NetCalendy.Api/Controllers/BookingsController.cs) | Controller | Zero logic; `mediator.Send` + `result.ToActionResult` |
| 18 | [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs) | Startup wiring | Service registration order; middleware order |

---

## Mid-Level Track (Files 19–27)

| # | File | Why now | What to look for |
|---|---|---|---|
| 19 | [`src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs`](../../src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs) | Multi-tenancy | Header vs subdomain resolution; what happens if tenant not found |
| 20 | [`src/NetCalendy.Api/Middleware/ExceptionHandlingMiddleware.cs`](../../src/NetCalendy.Api/Middleware/ExceptionHandlingMiddleware.cs) | Error handling | How `ValidationException` → 400, unhandled → 500 |
| 21 | [`src/NetCalendy.Api/Extensions/ResultExtensions.cs`](../../src/NetCalendy.Api/Extensions/ResultExtensions.cs) | Result → HTTP mapping | The `switch` expression that maps error code suffix to status code |
| 22 | [`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs) | Complex query | Timezone conversion, conflict filtering, slot generation |
| 23 | [`src/NetCalendy.Infrastructure/Services/CurrentTenantService.cs`](../../src/NetCalendy.Infrastructure/Services/CurrentTenantService.cs) | Tenant from JWT | How claims are read; null return path |
| 24 | [`src/NetCalendy.Infrastructure/DependencyInjection.cs`](../../src/NetCalendy.Infrastructure/DependencyInjection.cs) | DI wiring | How repositories are registered; `AddHttpContextAccessor` |
| 25 | [`tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs) | Integration test setup | `ConfigureWebHost` DI override; Testcontainers lifecycle |
| 26 | [`tests/NetCalendy.Application.Tests/Bookings/CreateBookingHandlerTests.cs`](../../tests/NetCalendy.Application.Tests/Bookings/CreateBookingHandlerTests.cs) | Handler testing pattern | NSubstitute `.Returns()`; mock verification; test naming |
| 27 | [`web/src/components/booking/BookingPage.tsx`](../../web/src/components/booking/BookingPage.tsx) | React flow | `useState` for step machine; React Query data; async form submission |

---

## Senior Track (Files 28–35)

| # | File | Why now | What to look for |
|---|---|---|---|
| 28 | [`src/NetCalendy.Domain/Common/IDomainEvent.cs`](../../src/NetCalendy.Domain/Common/IDomainEvent.cs) | Domain event contract | Why it extends `INotification`; what implementing MediatR here means for the Domain's dependency policy |
| 29 | [`src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/RepositoryBase.cs) | Repo base class | Why `FindAsync([id], ct)` instead of `.FirstOrDefaultAsync(x => x.Id == id)` — performance |
| 30 | [`src/NetCalendy.Infrastructure/Persistence/Configurations/ScheduleConfiguration.cs`](../../src/NetCalendy.Infrastructure/Persistence/Configurations/ScheduleConfiguration.cs) | Owned collection mapping | How `OwnsMany` maps `AvailabilityRule` list to a separate table with shadow key `id` |
| 31 | [`src/NetCalendy.Application/DependencyInjection.cs`](../../src/NetCalendy.Application/DependencyInjection.cs) | Pipeline registration | Order of pipeline behaviors; why `LoggingBehavior` is registered before `ValidationBehavior` |
| 32 | [`src/NetCalendy.Domain/Schedules/TimeRange.cs`](../../src/NetCalendy.Domain/Schedules/TimeRange.cs) | Value object depth | Immutability, `Overlaps` method, `GetEqualityComponents` |
| 33 | [`src/NetCalendy.Domain/Users/Email.cs`](../../src/NetCalendy.Domain/Users/Email.cs) | Value object with normalization | Trim + lowercase in factory; why this belongs in the domain |
| 34 | [`web/src/api/client.ts`](../../web/src/api/client.ts) | Axios config | Interceptors: token injection + 401 auto-redirect |
| 35 | [`web/src/hooks/useEventTypes.ts`](../../web/src/hooks/useEventTypes.ts) | React Query patterns | Cache invalidation on mutation; query key structure |
