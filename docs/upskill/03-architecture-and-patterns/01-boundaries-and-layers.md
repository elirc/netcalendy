# 01 — Boundaries and Layers

## The Dependency Rule

Every import arrow must point **inward**:

```
API → Application → Domain
Infrastructure → Application → Domain
```

Domain has no outward imports (except `MediatR.Contracts` for `INotification` — a debatable tradeoff discussed below).

Application imports Domain only. It does not import `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, or any infrastructure NuGet package.

Infrastructure imports Application (to implement its interfaces). It knows about EF Core, Npgsql, and `HttpContext`.

API imports Application (to send commands) and Infrastructure (only to register DI in `Program.cs`).

---

## What Each Layer Owns (and Must Not Own)

### Domain
**Owns:** Aggregates, value objects, domain events, entity IDs, `Result<T>`, `Error`, repository interfaces.

**Must not own:** Database queries, HTTP concepts, DI framework, email sending, configuration files.

**Boundary test:** If you add `using Microsoft.EntityFrameworkCore` to a Domain file, the dependency rule is violated.

**The MediatR.Contracts tradeoff:** `IDomainEvent` extends `INotification` (from MediatR.Contracts). This creates a dependency from Domain to a third-party library. The alternative — defining `IDomainEvent` without `INotification` and adapting it in Infrastructure — avoids the coupling but adds a mapping layer. The current choice is pragmatic and common in the community but worth knowing.

---

### Application
**Owns:** Use cases (commands, queries, handlers), FluentValidation validators, pipeline behaviors, abstract service interfaces (`IUnitOfWork`, `ICurrentUserService`, `ICurrentTenantService`, `IDateTimeProvider`).

**Must not own:** `DbContext`, HTTP routing, `HttpContext`, specific DI container APIs.

**Boundary test:** A handler that injects `AppDbContext` directly violates this boundary. It should inject `IRepository<T>` and `IUnitOfWork` only.

---

### Infrastructure
**Owns:** `AppDbContext`, EF entity configurations, repository implementations, `CurrentTenantService`, migrations.

**Must not own:** Business invariants (e.g., "a booking can't be in the past" — that's domain's job), controller logic.

---

### API
**Owns:** Route definitions, HTTP response shaping, middleware, auth configuration, startup.

**Must not own:** Business logic (no `if` conditions about whether a booking is valid), database queries.

**The thin controller pattern:** Every controller action is: `var result = await mediator.Send(command, ct); return result.ToActionResult();`. Zero logic. See [`BookingsController.cs:19`](../../src/NetCalendy.Api/Controllers/BookingsController.cs#L19).

---

## Boundary Leak Examples

### Leak 1: DTO Tied to Entity Shape
When a command like `CreateEventTypeCommand` mirrors exactly the `EventType` entity's properties, you've implicitly coupled the API contract to the database model. If you add a column to the table, you're tempted to add a field to the command — and now the database schema drives the API shape. The fix: design commands around user intent, not around entity structure.

### Leak 2: `AppDbContext` in a Handler
```csharp
// Illustrative fake code: not from this repo
public class BadHandler(AppDbContext db) : IRequestHandler<...>
{
    public async Task<...> Handle(...)
    {
        var bookings = await db.Bookings.Where(...).ToListAsync(); // crosses boundary
    }
}
```
The handler now knows EF Core exists. You can't test it without an `AppDbContext`. Fix: inject `IBookingRepository`.

### Leak 3: Business Logic in a Controller
```csharp
// Illustrative fake code: not from this repo
[HttpPost]
public async Task<IActionResult> Book([FromBody] BookRequest req)
{
    if (req.StartTime < DateTime.UtcNow) return BadRequest("Can't book in the past");
    // This invariant belongs in Booking.Create()
}
```

---

## How to Spot Boundary Violations in Code Review

1. Does a Domain class import anything other than `MediatR.Contracts` or BCL?
2. Does an Application handler import `EntityFrameworkCore`, `AspNetCore`, or `Npgsql`?
3. Does a controller have business logic (`if`, validation rules, calculation)?
4. Does an Infrastructure class contain a domain invariant?

🔧 **Drill:** Open each `*.csproj` file and list the `<PackageReference>` elements. Verify that `NetCalendy.Application` has no EF Core package. Verify that `NetCalendy.Domain` has no MediatR handler package (only `MediatR.Contracts`).
