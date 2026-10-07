# 05 — Pattern Catalog

18 patterns used in this codebase. Each card: problem → general shape → real example → failure modes → drill.

---

## Pattern 01: Result<T> / Railway-Oriented Returns

**Problem:** Exceptions for control flow are expensive, swallowed silently, and force callers to guess what can go wrong. `null` returns lose context.

**General shape:** Every operation that can fail returns `Result<T>` (or `Result` for void). Callers check `IsFailure` before accessing `Value`. Errors carry a `Code` and `Description`.

**Real example:** [`src/NetCalendy.Domain/Common/Result.cs`](../../../src/NetCalendy.Domain/Common/Result.cs) — constructor enforces the invariant: success + error is illegal.

**Second:** [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs:23`](../../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs#L23) — early return pattern: `if (eventType is null) return Result.Failure<...>(Error.NotFound(...))`.

**Why it works:** All failure modes are visible in the method signature. Callers cannot ignore failures. No exception overhead for expected failure paths.

**Failure modes:**
- Accessing `result.Value` when `IsFailure` is true throws `InvalidOperationException`
- Mapping error codes to HTTP status codes inconsistently — see `ResultExtensions.cs` for the current mapping logic

**Use when:** Any operation with defined failure modes.

**Avoid when:** Truly unexpected exceptions (infrastructure down, out-of-memory) — those should propagate as exceptions.

🔧 **Drill:** Add a `Match<T>(Func<TValue, T> onSuccess, Func<Error, T> onFailure)` method to `Result<TValue>`. What does it replace in calling code?

---

## Pattern 02: Aggregate with Private Constructor

**Problem:** Objects can be created in invalid states if construction logic is scattered across the codebase.

**General shape:** Constructor is `private`. A static `Create(...)` factory method validates inputs, calls the constructor, raises domain events, and returns `Result<T>`.

**Real example:** [`src/NetCalendy.Domain/Bookings/Booking.cs:17`](../../../src/NetCalendy.Domain/Bookings/Booking.cs#L17) — private constructor at line 17; public `Create` at line 37.

**Second:** [`src/NetCalendy.Domain/Tenants/Tenant.cs:14`](../../../src/NetCalendy.Domain/Tenants/Tenant.cs#L14)

**Why it works:** Invariants are enforced at the only creation site. Domain events are guaranteed to fire. Test code must use the factory, so tests exercise the same path as production.

**Failure modes:**
- EF Core needs a way to rehydrate entities from the database — the constructor must be accessible to EF's proxy/factory. Currently EF Core uses the private constructor via reflection. If EF can't find the right constructor, you get a runtime error, not a compile error.

**Use when:** Any aggregate root or entity where invariants must be enforced at creation.

**Avoid when:** Simple configuration objects or DTOs.

🔧 **Drill:** What happens if you add a `public Booking()` no-arg constructor? Does the factory invariant still hold? Write a test that proves it.

---

## Pattern 03: Value Object with Equality-by-Content

**Problem:** Two `Email` objects with the same address should be equal. Class reference equality doesn't support this.

**General shape:** Inherit from `ValueObject`. Override `GetEqualityComponents()`. Immutable properties only.

**Real example:** [`src/NetCalendy.Domain/Users/Email.cs`](../../../src/NetCalendy.Domain/Users/Email.cs) — `Value` property, static `Create` with normalization, `GetEqualityComponents` yields `Value`.

**Second:** [`src/NetCalendy.Domain/Schedules/TimeRange.cs`](../../../src/NetCalendy.Domain/Schedules/TimeRange.cs)

**Why it works:** Two independent `Email.Create("Alice@Example.COM")` calls produce equal objects. Safe to use as dictionary keys. Normalization (lowercased) lives in one place.

**Failure modes:**
- EF Core maps value objects via `OwnsOne` / `OwnsMany`. If you forget the configuration, EF silently ignores the owned type and you lose data.
- Adding a mutable property to a value object breaks equality semantics.

🔧 **Drill:** Add `PhoneNumber` as a value object for `Invitee`. Where do you add it? What changes in domain, EF config, and command/validator?

---

## Pattern 04: Strong-Typed IDs

**Problem:** `GetById(Guid tenantId)` compiles when you accidentally pass `userId`. Wrong-argument bugs are runtime failures.

**General shape:** `record TenantId(Guid Value)` with `New()` and `From(Guid)` factory methods.

**Real example:** [`src/NetCalendy.Domain/Tenants/TenantId.cs`](../../../src/NetCalendy.Domain/Tenants/TenantId.cs) — also see EF value converter in [`TenantConfiguration.cs:10`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/TenantConfiguration.cs#L10).

**Why it works:** `UpdateUser(UserId id, TenantId tenantId)` is impossible to call with the arguments swapped.

**Failure modes:**
- Value converters must be registered in EF config for every ID type. If you add a new aggregate and forget the converter, EF stores the whole `record` as a string rather than the inner `Guid`.
- Serialization: `System.Text.Json` won't serialize `record TenantId(Guid Value)` as a plain GUID — you'd get `{"value":"..."}`. You need a custom `JsonConverter` or convert to `Guid` at the API boundary.

🔧 **Drill:** Check `BookingsController.cs` — where is `BookingId.From(...)` called vs where is a raw `Guid` used? Is the boundary clean?

---

## Pattern 05: CQRS Command + Handler (MediatR)

**Problem:** Mixing read and write logic in the same service creates a God Object. Commands and queries have different optimization needs.

**General shape:** Command is a `record` implementing `IRequest<Result<TResponse>>`. Handler implements `IRequestHandler<TCommand, TResult>`. MediatR routes by type.

**Real example:** [`src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeCommand.cs`](../../../src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeCommand.cs) — command + response in one file.

**Second:** [`src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeHandler.cs`](../../../src/NetCalendy.Application/EventTypes/Commands/CreateEventType/CreateEventTypeHandler.cs)

**Why it works:** Each handler is a single-responsibility unit. Test in isolation with mocked dependencies. Add new behavior by adding a new handler, not modifying existing ones.

**Failure modes:**
- Commands are sent via `mediator.Send()` — if no handler is registered, MediatR throws at runtime (not compile time).
- Large handlers grow into mini-services. Watch for handlers that do 5+ things.

🔧 **Drill:** What happens if you accidentally register two handlers for the same command? MediatR behavior is to throw — verify this understanding.

---

## Pattern 06: MediatR Pipeline Behavior

**Problem:** Every handler needs validation and logging. Repeating this in every handler is duplication and easy to forget.

**General shape:** `IPipelineBehavior<TRequest, TResponse>` wraps the handler call. Registered in DI in order.

**Real example:** [`src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs`](../../../src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs) — collects all `IValidator<TRequest>` instances, runs them, throws on failures.

**Second:** [`src/NetCalendy.Application/Common/Behaviors/LoggingBehavior.cs`](../../../src/NetCalendy.Application/Common/Behaviors/LoggingBehavior.cs)

**Registration order:** [`DependencyInjection.cs:13-14`](../../../src/NetCalendy.Application/DependencyInjection.cs#L13) — `LoggingBehavior` before `ValidationBehavior`. This means: log the request → validate → execute handler → log completion.

**Failure modes:**
- `ValidationBehavior` throws `FluentValidation.ValidationException`. If `ExceptionHandlingMiddleware` is not in the pipeline, this bubbles as a 500.
- Order matters: putting `LoggingBehavior` after `ValidationBehavior` means validation failures aren't logged with timing.

---

## Pattern 07: Repository Pattern

**Problem:** Application layer shouldn't know about EF Core. Infrastructure shouldn't contain business logic.

**General shape:** Interface in Domain (`IBookingRepository`). Implementation in Infrastructure (`BookingRepository`). Base class handles common CRUD (`RepositoryBase<T, TId>`).

**Real example:** [`src/NetCalendy.Domain/Repositories/IBookingRepository.cs`](../../../src/NetCalendy.Domain/Repositories/IBookingRepository.cs) — interface.

**Second:** [`src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs`](../../../src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs) — implementation.

**Why it works:** Application tests can use `NSubstitute` to mock the repository without needing a database.

**Failure modes:**
- Repositories leaking IQueryable outside their class — callers can add `.Where()` after the fact, defeating the abstraction.
- Unit of Work and Repository not being aligned — calling `Add()` multiple times but `SaveChangesAsync()` once is the expected pattern; calling `SaveChangesAsync()` inside the repository breaks transactional boundaries.

---

## Pattern 08: Unit of Work (IUnitOfWork)

**Problem:** Multiple repositories in one handler need to commit atomically.

**General shape:** `IUnitOfWork.SaveChangesAsync()` flushes all pending changes from the EF change tracker in one transaction.

**Real example:** [`src/NetCalendy.Application/Common/IUnitOfWork.cs`](../../../src/NetCalendy.Application/Common/IUnitOfWork.cs) — single-method interface.

**Second:** [`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs:31`](../../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs#L31) — implementation in `AppDbContext`.

**Why it works:** `AppDbContext` is scoped per request. All repositories injected in the same request share the same `DbContext` instance — so one `SaveChangesAsync()` commits all their changes.

**Failure modes:**
- Injecting `AppDbContext` directly in handlers instead of `IUnitOfWork` breaks the abstraction and makes the handler Infrastructure-dependent.
- Calling `SaveChangesAsync()` multiple times in one handler can produce partial commits if the second call fails.

---

## Pattern 09: EF Core Owned Types for Value Objects

**Problem:** Value objects have no table of their own — they live inside their owning entity's row (or a sibling table for collections).

**General shape:** `builder.OwnsOne(e => e.Duration, d => d.Property(x => x.Minutes).HasColumnName(...))` for single-value VOs. `builder.OwnsMany(s => s.Rules, rules => { ... })` for collections.

**Real example:** [`src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs:40`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs#L40) — `Invitee` mapped via `OwnsOne`.

**Second:** [`src/NetCalendy.Infrastructure/Persistence/Configurations/ScheduleConfiguration.cs:33`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/ScheduleConfiguration.cs#L33) — `Rules` mapped via `OwnsMany` to `schedule_rules` table.

**Failure modes:**
- Forgetting `OwnsOne/Many` — EF Core will create a separate table with an automatically-generated key, or silently fail to persist.
- `OwnsMany` creates a shadow property `id` as PK — without `.HasKey("id")` this can fail at migration time.

---

## Pattern 10: Tenant-Scoped Query Pattern

**Problem:** A query that forgets `WHERE tenant_id = ?` accidentally returns another tenant's data (IDOR vulnerability).

**General shape:** Every repository query includes a `TenantId` filter parameter. Optionally: EF global query filters.

**Real example:** [`src/NetCalendy.Infrastructure/Persistence/Repositories/EventTypeRepository.cs:12`](../../../src/NetCalendy.Infrastructure/Persistence/Repositories/EventTypeRepository.cs#L12) — `Where(e => e.OwnerId == ownerId && e.TenantId == tenantId)`.

**Second:** [`src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs:22`](../../../src/NetCalendy.Infrastructure/Persistence/Repositories/BookingRepository.cs#L22) — `ListByHostAsync` filters by `tenantId`.

**Failure modes:**
- `GetById` in `RepositoryBase` does NOT filter by tenant — it loads by primary key only. A caller who knows another tenant's booking ID can retrieve it. This is a real vulnerability (see risk register RISK-001).
- No global EF query filter means each repository method must be manually audited.

---

## Pattern 11: ExceptionHandlingMiddleware (Global Exception Boundary)

**Problem:** Unhandled exceptions reach the default ASP.NET error handler, which may expose stack traces in production.

**General shape:** First middleware in the pipeline. Catches `ValidationException` → 400, all others → 500. Logs the exception.

**Real example:** [`src/NetCalendy.Api/Middleware/ExceptionHandlingMiddleware.cs`](../../../src/NetCalendy.Api/Middleware/ExceptionHandlingMiddleware.cs)

**Failure modes:**
- Exceptions thrown by response-writing code after `await next()` may be swallowed silently since the response is already started.
- Logging personally identifiable information from exceptions in production.

---

## Pattern 12: JWT Claims as Current Context

**Problem:** Handlers need to know "who is calling and for which tenant" without passing those values through every method.

**General shape:** `ICurrentUserService` and `ICurrentTenantService` read from `IHttpContextAccessor`. Registered scoped. Injected into handlers.

**Real example:** [`src/NetCalendy.Infrastructure/Services/CurrentTenantService.cs`](../../../src/NetCalendy.Infrastructure/Services/CurrentTenantService.cs)

**Failure modes:**
- If used outside of an HTTP request (background job, test without request context), `HttpContext` is null — `TenantId` returns null and the handler throws.
- Captive dependency: if you inject a scoped service into a singleton, it gets the first request's context forever.

---

## Pattern 13: FluentValidation Validator Colocated with Command

**Problem:** Validation rules scattered across controllers, handlers, and domain make them hard to find and audit.

**General shape:** One `AbstractValidator<TCommand>` per command, in the same folder as the command.

**Real example:** [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs`](../../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs)

**Failure modes:**
- Two validation layers (FV + domain factory) checking the same rule inconsistently — e.g., FV allows 500 chars for a field that the domain allows 480.
- `UpdateEventType` command has no validator (investigate — `UpdateEventTypeCommand.cs` has no paired validator file).

---

## Pattern 14: Domain Events Dispatched Post-Save

**Problem:** Side effects (send email, call webhook) shouldn't run if the database transaction fails.

**General shape:** Aggregate calls `RaiseDomainEvent(event)` during state changes. `AppDbContext.SaveChangesAsync` collects all pending events, saves to DB, then dispatches via `IPublisher`.

**Real example:** [`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs:31-42`](../../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs#L31)

**Failure modes:**
- If `publisher.Publish` throws (e.g., no handler found throws), the exception propagates after the database write already succeeded. The state is saved but the side effect never ran. There's no retry/outbox.
- No `INotificationHandler` implementations exist — events currently have no effect.

---

## Pattern 15: React Query for Server State

**Problem:** Manual `useState` + `useEffect` for API calls creates loading/error/stale state management boilerplate.

**General shape:** `useQuery({ queryKey, queryFn })` for reads. `useMutation({ mutationFn, onSuccess: qc.invalidateQueries(...) })` for writes.

**Real example:** [`web/src/hooks/useEventTypes.ts`](../../../web/src/hooks/useEventTypes.ts) — `useEventTypes`, `useDeleteEventType`, `useCreateEventType`.

**Failure modes:**
- Stale cache: if `queryKey` doesn't change when the underlying data changes, React Query returns cached data forever.
- Mutation without cache invalidation — the UI shows old data after a write.

---

## Pattern 16: Multi-Step Form State Machine (React)

**Problem:** Multi-step forms with shared state and conditional rendering are hard to manage with scattered boolean flags.

**General shape:** Single `step` state variable with typed values (`'pick-slot' | 'fill-form' | 'confirmed'`). Render returns different JSX based on step. Transitions are explicit `setStep(...)` calls.

**Real example:** [`web/src/components/booking/BookingPage.tsx:20-26`](../../../web/src/components/booking/BookingPage.tsx#L20)

**Failure modes:**
- Back button in browser doesn't mirror the step state — pressing browser back while on 'fill-form' routes away entirely instead of going to 'pick-slot'.

---

## Pattern 17: Test Fixture + Factory with Testcontainers

**Problem:** Integration tests need a real database but shouldn't depend on a persistent dev database.

**General shape:** `IAsyncLifetime` class starts a Docker container in `InitializeAsync`, exposes the connection string, stops in `DisposeAsync`. `WebApplicationFactory.ConfigureWebHost` overrides the DI registration.

**Real example:** [`tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`](../../../tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs)

**Failure modes:**
- No database reset between test methods in the same class — residual data can cause false negatives.
- Test assumes Docker is running — CI must have Docker available.

---

## Pattern 18: Axios Interceptors for Auth and Error Handling

**Problem:** Adding the Bearer token to every request is boilerplate. 401 handling (redirect to login) should be centralized.

**General shape:** `apiClient.interceptors.request.use(...)` injects token from localStorage. `apiClient.interceptors.response.use(_, err => ...)` handles 401.

**Real example:** [`web/src/api/client.ts:7-19`](../../../web/src/api/client.ts#L7)

**Failure modes:**
- Token in `localStorage` is accessible to JavaScript — XSS vulnerability. HttpOnly cookies are safer.
- Auto-redirect on 401 in the interceptor fires for public endpoints that don't need a token but get a 401 anyway (e.g., `GET /api/event-types/:id` which is incorrectly `[Authorize]`).
