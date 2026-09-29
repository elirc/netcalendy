# 03 — Domain Glossary

Every noun in the system, where it lives, and what invariants or rules apply to it.

## Core Domain Nouns

### Tenant
**File:** [`src/NetCalendy.Domain/Tenants/Tenant.cs`](../../src/NetCalendy.Domain/Tenants/Tenant.cs)  
**What it is:** The top-level isolation boundary. Every other object belongs to a tenant. Think "organization" or "company account."  
**Key rules:** Slug must be `^[a-z0-9]+(?:-[a-z0-9]+)*$` and globally unique. Active/inactive flag.  
**Where it appears:** Every entity that is tenant-scoped carries a `TenantId` column. Repository queries filter by it.  
**ID type:** [`TenantId`](../../src/NetCalendy.Domain/Tenants/TenantId.cs) — a `record` wrapping `Guid`.

---

### User
**File:** [`src/NetCalendy.Domain/Users/User.cs`](../../src/NetCalendy.Domain/Users/User.cs)  
**What it is:** A person who belongs to a tenant. In booking parlance, the **host** — the person who owns event types and receives bookings.  
**Key rules:** Email is a value object ([`Email.cs`](../../src/NetCalendy.Domain/Users/Email.cs)) — normalized to lowercase, validated by regex. Email must be unique within a tenant (not globally). Timezone is a plain string (validated non-empty; full IANA validation is a known gap — see risk register).  
**Roles:** `Member`, `Admin`, `Owner` ([`UserRole.cs`](../../src/NetCalendy.Domain/Users/UserRole.cs)).

---

### EventType
**File:** [`src/NetCalendy.Domain/EventTypes/EventType.cs`](../../src/NetCalendy.Domain/EventTypes/EventType.cs)  
**What it is:** A bookable meeting format. "30-minute discovery call." Has a name, slug, duration, color, and optional description.  
**Key rules:** Slug must be unique per owner within the tenant. Duration is a value object ([`Duration.cs`](../../src/NetCalendy.Domain/EventTypes/Duration.cs)) — max 480 minutes. `IsActive` flag; deactivation soft-deletes.  
**Public-facing:** Invitees see event types via a public URL containing the host's slug and event type slug.

---

### Schedule
**File:** [`src/NetCalendy.Domain/Schedules/Schedule.cs`](../../src/NetCalendy.Domain/Schedules/Schedule.cs)  
**What it is:** A user's availability template — which days/hours they accept bookings. Default is Mon–Fri 09:00–17:00 UTC.  
**Key rules:** Owns a list of `AvailabilityRule` value objects, each linking a `DayOfWeek` to a `TimeRange`. `TimeRange.Start` must be strictly before `TimeRange.End`. Multiple rules per day are allowed (e.g., 9–12 and 14–17).  
**Slot generation:** `Schedule.GetAvailableSlots(date, durationMinutes)` produces `TimeSlot` records by walking the day's window in `durationMinutes` increments — see [`Schedule.cs:53`](../../src/NetCalendy.Domain/Schedules/Schedule.cs#L53).

---

### Booking
**File:** [`src/NetCalendy.Domain/Bookings/Booking.cs`](../../src/NetCalendy.Domain/Bookings/Booking.cs)  
**What it is:** A confirmed appointment between a host (user) and an invitee (external person — no account required).  
**Key rules:**
- `StartTime` must be in the future at creation time — [`Booking.cs:47`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L47)
- `StartTime < EndTime` — [`Booking.cs:44`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L44)
- `CancellationToken` is a 16-char hex string auto-generated at creation — allows invitees to cancel without auth
- Cancellation is idempotent-protected: cancelling an already-cancelled booking returns `Error.Conflict("Booking.AlreadyCancelled")` — [`Booking.cs:58`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L58)
- `ConflictsWith(start, end)` returns false for cancelled bookings — [`Booking.cs:67`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L67)

---

## Value Objects

| Name | File | Rule |
|---|---|---|
| `Email` | [`Users/Email.cs`](../../src/NetCalendy.Domain/Users/Email.cs) | Lowercase, `@` present, max 254 chars |
| `Duration` | [`EventTypes/Duration.cs`](../../src/NetCalendy.Domain/EventTypes/Duration.cs) | 1–480 minutes |
| `TimeRange` | [`Schedules/TimeRange.cs`](../../src/NetCalendy.Domain/Schedules/TimeRange.cs) | Start < End |
| `AvailabilityRule` | [`Schedules/AvailabilityRule.cs`](../../src/NetCalendy.Domain/Schedules/AvailabilityRule.cs) | DayOfWeek + valid TimeRange |
| `Invitee` | [`Bookings/Invitee.cs`](../../src/NetCalendy.Domain/Bookings/Invitee.cs) | Non-empty name, valid email, non-empty timezone |

Value objects are **equality-by-content** (not by identity), immutable, and enforce their own invariants via static factory. See [`Common/ValueObject.cs`](../../src/NetCalendy.Domain/Common/ValueObject.cs) for the base.

---

## Strong-Typed IDs

Every aggregate uses a `record` ID type instead of a raw `Guid`. Example: `TenantId`, `UserId`, `EventTypeId`, `ScheduleId`, `BookingId`.

**Why:** Prevents passing `userId` where `tenantId` is expected. The compiler catches it. EF Core uses value converters to map them to/from `Guid` columns.

**Pattern:**
```csharp
// All ID types look like this (TenantId shown):
public record TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId From(Guid value) => new(value);
}
```

---

## Domain Events

| Event | Raised by | When |
|---|---|---|
| `TenantCreatedEvent` | `Tenant.Create()` | New tenant |
| `UserRegisteredEvent` | `User.Create()` | New user |
| `EventTypeCreatedEvent` | `EventType.Create()` | New event type |
| `BookingCreatedEvent` | `Booking.Create()` | Booking confirmed |
| `BookingCancelledEvent` | `Booking.Cancel()` | Booking cancelled |

Events are dispatched **after** `SaveChanges` succeeds, via `IPublisher` in `AppDbContext`. No handlers exist yet (investigate: no `INotificationHandler<BookingCreatedEvent>` is registered — email notification is a future feature).

---

## Application Concepts

| Term | Meaning |
|---|---|
| **Command** | A `IRequest<Result<T>>` that changes state. Named `*Command`. |
| **Query** | A `IRequest<Result<T>>` that reads state. Named `*Query`. |
| **Handler** | A `IRequestHandler<TCommand, TResult>` that executes one command or query. |
| **Pipeline behavior** | Code that runs around every handler — validation, logging. |
| **Unit of work** | `IUnitOfWork` (implemented by `AppDbContext`) — flushes changes to the database in one transaction. |

---

## Multi-Tenancy Terms

| Term | Meaning | Enforcement point |
|---|---|---|
| **Tenant slug** | Human-readable identifier for a tenant (e.g., `acme-corp`) | `TenantResolutionMiddleware` |
| **Tenant ID** | Guid that all tenant-scoped rows carry | EF configurations, JWT claims |
| **Tenant claim** | `tenant_id` and `tenant_slug` in the JWT | `CurrentTenantService` reads `IHttpContextAccessor` |
| **Cross-tenant query** | A query that accidentally reads another tenant's data | Risk: no global EF query filter yet (see risk register) |
