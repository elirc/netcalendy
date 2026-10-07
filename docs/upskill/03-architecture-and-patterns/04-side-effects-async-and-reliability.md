# 04 — Side Effects, Async, and Reliability

## Domain Events: The Current State

Domain events are raised during aggregate state changes and dispatched via `IPublisher` (MediatR) after `SaveChanges` succeeds. See [`AppDbContext.cs:31-42`](../../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs#L31).

**Current handlers: none.** Events fire but nothing consumes them. This is scaffolding for future notification/webhook behavior.

**What this means today:** Booking created → `BookingCreatedEvent` published → MediatR finds no handler → no-op. Safe.

**What this means when you add a handler:** The handler runs **synchronously in the same HTTP request**. A handler that sends an email will:
1. Block the HTTP response until the email send completes (or times out)
2. If it throws, the `publisher.Publish` call throws, and the exception propagates from `SaveChangesAsync` — but the booking was already written. You get a saved booking with no confirmation email sent, and no retry mechanism.

---

## The Outbox Problem

**Problem:** You want durable side effects: "send an email when a booking is created, reliably, even if the process crashes between the DB write and the email send."

**Current architecture:** No outbox. Side effects are at-most-once.

**The fix (future work):** Write the event to an `outbox` table in the same transaction as the booking. A background job reads the outbox and processes events. If processing fails, it retries. This is the Transactional Outbox pattern.

**Pragmatic short-term option:** Use Hangfire's `IBackgroundJobClient.Enqueue(...)` inside the domain event handler. Not perfectly reliable (job can be queued but app crashes before executing), but handles the most common failure cases.

---

## Async/Await Patterns in This Repo

### CancellationToken Threading
Every handler accepts `CancellationToken ct` and passes it to every async call. This is correct. If the client disconnects, EF Core respects the token and abandons the database query.

Example: [`CreateBookingHandler.cs:17`](../../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs#L17) — `ct` flows from the controller through MediatR through the handler through `SaveChangesAsync`.

### ConfigureAwait
No `ConfigureAwait(false)` in this codebase. In ASP.NET Core, this is generally fine — the ASP.NET Core synchronization context doesn't cause the classic deadlock. The classic deadlock occurs only in older ASP.NET (non-Core) or WinForms/WPF when you mix `.Result` or `.Wait()` with `async/await`.

### No Sync-Over-Async
No `.Result` or `.Wait()` calls visible in the production code. This is correct. Sync-over-async can deadlock in non-Core contexts and always blocks a thread.

---

## External API Calls

No `IHttpClientFactory` or `HttpClient` usage in the current codebase. All operations are internal (DB only). When external API calls are added (e.g., Google Calendar integration, Stripe billing):

1. Register with `services.AddHttpClient<ICalendarClient, GoogleCalendarClient>()` to use named clients with DI-managed lifetimes
2. Add Polly retry/circuit-breaker policies via `AddPolicyHandler(...)`
3. Set `Timeout` on the handler — never leave external calls without a timeout
4. Add the call to the domain event handler, not the command handler — keeps the command simple

---

## The `GetAvailableSlots` Memory Load

[`GetAvailableSlotsHandler.cs:38`](../../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs#L38) loads all bookings for the host in the date range into memory. For a host with 100 bookings in the next 60 days, this is fine. For a host with 10,000 bookings, this loads 10,000 rows.

**Current risk:** Low (new system, few bookings). **Future risk:** High if hosts use the system heavily.

**Fix direction:** Push the slot-availability calculation to SQL — a CTE or stored procedure that computes available slots server-side. Or: paginate the available slots query and limit the date range (already limited to 60 days in the controller).

---

## Reliability Gaps Summary

| Gap | Location | Impact | Fix |
|---|---|---|---|
| No outbox for domain events | `AppDbContext.cs:42` | At-most-once side effects | Transactional outbox or Hangfire |
| No retry on `SaveChangesAsync` failure | All handlers | Transient DB errors return 500 | Polly retry around save, or EF Core `EnableRetryOnFailure` (already configured in Infrastructure DI) |
| No circuit breaker for external calls | N/A (no external calls yet) | Cascading failures | Polly circuit breaker when external calls are added |
| Concurrent booking (no DB lock) | `CreateBookingHandler.cs:30` | Double-booking | Unique partial index on `(host_id, start_time)` |

Note: `EnableRetryOnFailure(3)` **is** configured in [`DependencyInjection.cs`](../../../src/NetCalendy.Infrastructure/DependencyInjection.cs) — Npgsql will retry transient errors up to 3 times before failing.
