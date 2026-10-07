# 04 — Performance Thinking

## The Four Questions

Before optimizing anything, answer:
1. **Is this actually slow?** Measure first. Don't optimize what you haven't profiled.
2. **Where is the bottleneck?** CPU, memory, database, or network?
3. **What is the expected scale?** 10 tenants or 10,000?
4. **What does "slow" mean to the user?** A 200ms API response is fine; a 3s booking page load is not.

---

## Hotspot 1: `GetAvailableSlots` — In-Memory Conflict Filtering

**File:** [`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)

**What it does:** Loads all bookings for a host within a date range, then filters in application memory to find free slots.

**Current query count:** 3 DB calls (load schedule, load event type, load bookings).

**The O(n×m) problem:**
```
For each slot candidate (e.g., 9:00, 9:30, 10:00, ... in a 60-day window = ~960 slots)
  For each booking in the host's calendar
    Check if slot.ConflictsWith(booking)
```

With 100 bookings and 960 candidate slots, that's 96,000 comparisons in memory. Still fast at today's scale. At 10,000 bookings (heavy user with years of history), it's 9.6M comparisons, loading 10,000 EF objects.

**Measurement approach:**
1. Add a stopwatch around the slot-filtering loop
2. Log `hostId`, `bookingCount`, `slotCount`, `elapsedMs`
3. Set an alert if `elapsedMs > 500`

**Fix direction (when needed):**

Option A — SQL window function:
```sql
-- Illustrative: generate slots as a CTE, LEFT JOIN bookings, filter where no overlap
WITH slots AS (
  SELECT generate_series(...) AS slot_start
)
SELECT s.slot_start
FROM slots s
LEFT JOIN bookings b ON b.host_id = @hostId AND b.start_time < (s.slot_start + @duration) AND b.end_time > s.slot_start
WHERE b.id IS NULL;
```

Option B — Load only conflicting booking ranges, not full entities:
```csharp
var busyRanges = await db.Bookings
    .Where(b => b.HostId == hostId && b.StartTime < rangeEnd && b.EndTime > rangeStart)
    .Select(b => new { b.StartTime, b.EndTime }) // not full objects
    .ToListAsync(ct);
```

**Current status:** Not a real problem yet. Monitor; optimize when measured.

---

## Hotspot 2: N+1 Queries — `ListByHostAsync` with Related Data

**Risk:** If any handler calls `bookings.ListByHostAsync(hostId)` and then accesses a navigation property (e.g., `booking.EventType`) in a loop, each navigation load is a separate SQL query.

**Example of N+1 risk:**
```csharp
// Illustrative — if this existed in a handler
var bookings = await _bookings.ListByHostAsync(hostId, ct); // 1 query
foreach (var b in bookings)
{
    var et = await _eventTypes.GetByIdAsync(b.EventTypeId, ct); // N queries
    // ...
}
```

**Prevention in this repo:** Repository methods use `.Include(...)` for navigations needed in the handler. If a handler needs related data, the repository method should eager-load it.

**How to detect N+1:**
- Enable EF Core query logging: all SQL statements log at `Debug` level via Serilog
- Use `MiniProfiler` or `Seq` to count SQL queries per request
- Count `SELECT` statements in the log for a single API call

**Current risk:** Low — `GetAvailableSlotsHandler` loads bookings as a flat list with no navigation access. `CreateBookingHandler` loads EventType explicitly (1 query), not via navigation.

---

## Hotspot 3: Unbounded List Queries

**Risk:** Any `GET /api/...` endpoint that lists records without pagination can return arbitrarily large responses.

**Identified unbounded queries:**
- `GET /api/bookings?hostId=...` — `ListByHostAsync` has no LIMIT
- `GET /api/event-types` — `ListByOwnerAsync` has no LIMIT

**Current scale:** Fine. One tenant with 100 event types is a 4KB response.
**Future scale:** A power user with 50,000 bookings would transfer 2MB+ of JSON on every load.

**Fix pattern:**
```csharp
// In query handler — add page/pageSize defaults
public record GetBookingsQuery(Guid HostUserId, int Page = 1, int PageSize = 25)
    : IRequest<Result<PagedResult<BookingDto>>>;

// In repository
.Skip((page - 1) * pageSize).Take(pageSize)
```

**Return a PagedResult:**
```csharp
// Illustrative
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
```

**Important:** Always apply pagination in SQL (`.Skip().Take()`), not in application memory (`.ToList()` then slice). SQL pagination avoids loading all records.

---

## Hotspot 4: EF Owned Types and Repeated Column Reads

**Context:** `Invitee` is an EF owned type, stored as columns on the `Bookings` table (`Invitee_Name`, `Invitee_Email`, `Invitee_Timezone`, `Invitee_Notes`). When you load a `Booking`, EF always loads the owned type columns — there's no lazy loading for owned types.

**When this matters:** If you only need `Booking.Id` and `Booking.StartTime` (e.g., for a conflict check), EF still reads all `Invitee_*` columns. This is usually fine (owned types are on the same table row), but in projections you can be explicit:

```csharp
// Projection to avoid loading full Booking objects in conflict check
var conflicts = await db.Bookings
    .Where(b => b.HostId == hostId && b.StartTime < end && b.EndTime > start && b.Status != BookingStatus.Cancelled)
    .Select(b => b.Id) // Only fetch IDs
    .ToListAsync(ct);
return conflicts.Count > 0; // bool check
```

**Pattern reference:** [`src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs)

---

## Performance Checklist Before Shipping a Feature

- [ ] Does the endpoint load unbounded lists? Add pagination.
- [ ] Does the handler access navigation properties in a loop? Use `.Include()` or project to DTOs.
- [ ] Does the handler do work in-memory that could be done in SQL?
- [ ] Does the endpoint load full entities when only IDs/counts are needed?
- [ ] Are database indexes defined for the columns used in `.Where()` filters?
  - See existing indexes in [`src/NetCalendy.Infrastructure/Persistence/Configurations/`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/)
  - Common need: `(tenant_id, host_id)` on bookings for host queries

---

## Reading EF Core SQL Logs

Enable SQL logging in development:
```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Every SQL statement will appear in the console output. Count them for a single API call. More than expected queries = N+1 or missing eager loading.
