# 02 — Data Model and Persistence

## Entity Relationship Map

```
tenants (1) ──────────────── (*) users
tenants (1) ──────────────── (*) event_types
tenants (1) ──────────────── (*) schedules
tenants (1) ──────────────── (*) bookings

users (1) ─────────────────── (*) event_types  (owner_id)
users (1) ─────────────────── (*) schedules    (user_id)
users (1) ─────────────────── (*) bookings     (host_id)

event_types (1) ──────────── (*) bookings     (event_type_id)
schedules (1) ─────────────── (*) schedule_rules  (OwnsMany)

bookings.invitee_* ──── (OwnsOne) Invitee value object columns
event_types.duration_minutes ── (OwnsOne) Duration value object
users.email ── (OwnsOne) Email value object
```

---

## Table and Column Mapping

Every entity has a matching `IEntityTypeConfiguration<T>` in [`src/NetCalendy.Infrastructure/Persistence/Configurations/`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/).

### bookings table (most complex)
| C# property | Column | Notes |
|---|---|---|
| `Id` | `id` (uuid) | PK; `BookingId` → `Guid` via value converter |
| `TenantId` | `tenant_id` (uuid) | FK-like; not declared as FK in EF to avoid cross-aggregate constraints |
| `EventTypeId` | `event_type_id` (uuid) | — |
| `HostId` | `host_id` (uuid) | — |
| `Invitee.Name` | `invitee_name` (varchar 150) | `OwnsOne` mapping |
| `Invitee.Email` | `invitee_email` (varchar 254) | |
| `Invitee.Timezone` | `invitee_timezone` (varchar 100) | |
| `Invitee.Notes` | `invitee_notes` (varchar 1000) | nullable |
| `StartTime` | `start_time` (timestamptz) | |
| `EndTime` | `end_time` (timestamptz) | |
| `Status` | `status` (int) | Enum as int |
| `CancellationToken` | `cancellation_token` (varchar 16) | Unique index |
| `CancellationReason` | `cancellation_reason` (varchar 500) | nullable |

**Indexes on bookings:**
- `UNIQUE (cancellation_token)`
- `INDEX (host_id, start_time)` — for conflict queries
- `INDEX (tenant_id, start_time)` — for tenant-scoped list queries

---

## EF Core Key Concepts Used

### Value Converters for Strong IDs
```
.HasConversion(id => id.Value, v => TenantId.From(v))
```
Seen in every configuration file. Maps the `record TenantId(Guid Value)` to/from a raw `Guid` at the database boundary.

### OwnsOne for Value Objects
```
builder.OwnsOne(b => b.Invitee, inv => {
    inv.Property(x => x.Name).HasColumnName("invitee_name")...
})
```
`Invitee` has no table. Its properties are columns in the `bookings` table.

### OwnsMany for Collections
```
builder.OwnsMany(s => s.Rules, rules => {
    rules.ToTable("schedule_rules");
    rules.WithOwner().HasForeignKey("schedule_id");
    rules.Property<int>("id").ValueGeneratedOnAdd();
    rules.HasKey("id");
    ...
})
```
`AvailabilityRule` objects live in `schedule_rules` table. The `id` column is a shadow property (not on the C# class).

---

## EF Core Lifetime: Why Scoped Matters

`AppDbContext` is registered as **scoped** — one instance per HTTP request. All repositories injected in the same request share the same `DbContext`. This means:
1. `eventTypeRepo.Add(et)` and `bookingRepo.Add(booking)` both register with the same change tracker
2. `uow.SaveChangesAsync()` flushes both in one transaction
3. The context is disposed at the end of the request — no leaked connections

**Danger:** Injecting `AppDbContext` as a singleton (or injecting it into a singleton service) creates a "captive dependency" — the context lives forever, accumulates entities, and eventually throws or returns stale data.

---

## Change Tracking

EF Core tracks every entity loaded via its repository. When `SaveChangesAsync()` is called:
- Added entities → `INSERT`
- Modified entities → `UPDATE` (only changed columns)
- Removed entities → `DELETE`

`repository.Update(aggregate)` calls `db.Set<T>().Update(aggregate)` which marks the entire entity as modified. This is a "full update" pattern — every column is written even if only one changed. For a production system handling high write volume, this can be improved with explicit property tracking.

---

## Migrations

**No migrations exist yet** — they must be generated after the first `dotnet ef migrations add`. See `src/NetCalendy.Infrastructure/Persistence/Migrations/README.md`.

### Safe Schema Change Checklist
When adding a column:
- [ ] New columns must be `nullable` or have a `DEFAULT` — `NOT NULL` without a default fails on existing rows
- [ ] Large tables: use `ADD COLUMN ... DEFAULT` (Postgres fills nulls inline for small tables; large tables need `DEFAULT` or online migration tool)
- [ ] Run `dotnet ef migrations script` to review the generated SQL before applying
- [ ] Test migration on a copy of production data before deploying

When removing a column:
- [ ] First deploy: stop reading the column (remove from EF config)
- [ ] Second deploy: add a migration to drop the column
- [ ] This two-step process prevents downtime from rolling deployment

---

## N+1 Query Risk

`GetAvailableSlotsHandler` loads all bookings for the host in the date range (one query), then checks each slot for conflicts in-memory (loop). This is O(slots × bookings) but avoids N+1 queries — it trades memory for database round trips.

A potential N+1 exists in `ScheduleRepository.GetDefaultByUserAsync` which uses `.Include("_rules")` — a string-based include for the shadow backing field. This should work but is fragile; if the backing field name changes, the include silently fails and `Rules` returns empty. **Investigate: verify the shadow property name matches the private field in `Schedule.cs`.**

---

## Transaction Boundaries

Each handler that writes calls `uow.SaveChangesAsync()` once at the end. EF Core wraps the save in an implicit transaction. This means: if you create a `User` and a `Schedule` in the same handler, both succeed or both fail.

**What's not covered:** If you call `uow.SaveChangesAsync()` twice in one handler (bad pattern), the first save commits permanently even if the second fails. The repo currently does not have this pattern, but it's a code review red flag.
