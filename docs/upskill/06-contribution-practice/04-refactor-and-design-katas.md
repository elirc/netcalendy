# Refactor and Design Katas

Each kata gives you a design problem or a refactoring challenge. These are open-ended — there is no single correct answer. The goal is to practice thinking through tradeoffs and communicating design decisions.

---

## Kata 1: Should Booking Duration Live in the Domain?

**Current state:** Booking duration is derived in `CreateBookingHandler`:
```csharp
// CreateBookingHandler.cs
var endTime = cmd.StartTime.AddMinutes(eventType.Duration.Minutes);
```

**Design question:**  
`endTime` is computed from `startTime + duration` in the application layer, then passed to `Booking.Create(startTime, endTime)`. The domain stores both `StartTime` and `EndTime`.

**Kata task:**  
Argue both sides of this design:

**Side A — Keep it as-is (application layer computes end time):**
- What is the benefit of the domain receiving explicit start and end?
- What invariants does the domain still enforce?
- What would break if the handler passed wrong end time?

**Side B — Let the domain compute end time from duration:**
- Change `Booking.Create(startTime, endTime)` to `Booking.Create(startTime, duration, eventType)` — the domain computes end time
- What does this gain?
- What does this lose?
- Is this over-coupling the booking to the event type?

**Write a 1-paragraph design decision** explaining which you'd choose and why, as if writing a PR description.

---

## Kata 2: Repository or Query Object?

**Current state:** All data access goes through repository interfaces:
```csharp
IBookingRepository.GetConflictingBookingsAsync(hostId, start, end, ct)
IBookingRepository.ListByHostAsync(hostId, ct)
```

**Design question:**  
Some teams use Query Objects instead of (or alongside) repository methods:
```csharp
// Illustrative: not in this repo
var query = new BookingConflictQuery(hostId, start, end);
var results = await _queryHandler.Execute(query, ct);
```

**Kata task:**

1. List two concrete scenarios in this codebase where a Query Object would be more expressive than a repository method.

2. List two scenarios where the repository pattern is preferable.

3. The `IBookingRepository` currently has 5 methods. If it grew to 20, what problems would that create? What refactoring would you suggest?

4. Write the signature (just the interface, no implementation) for a `GetAvailableBookingSlotsQuery` as a Query Object. What would `Execute(...)` return?

---

## Kata 3: Value Object or Entity?

**Scenarios to classify:** For each, decide whether it should be a Value Object or an Entity, and explain why.

1. **`CancellationToken` (string)** — A unique token assigned when a booking is created. Used to cancel without authentication.

2. **`TimeSlot` (start: DateTimeOffset, end: DateTimeOffset)** — A candidate available slot returned by `GetAvailableSlots`. Represents a time window, not a persisted record.

3. **`ScheduleRule` (dayOfWeek, startTime, endTime)** — One time window in a weekly schedule. Multiple rules per schedule.

4. **`WebhookDeliveryAttempt` (webhookId, attemptedAt, statusCode, responseBody)** — One attempt to deliver a webhook. Has a sequence number. Is retried on failure.

5. **`TenantSlug` (string)** — The URL-safe identifier for a tenant. Never changes after creation.

**For each:** State Value Object or Entity, and give one sentence explaining your reasoning.

---

## Kata 4: Design the Reschedule Feature

**Problem:** A host wants to reschedule a booking to a different time. Design the domain and application changes.

**Questions to answer:**

1. **Should `Booking` have a `Reschedule(newStart, newEnd)` method, or should you cancel-and-create?**
   - Argue both options. What domain events does each raise?
   - What is preserved vs. lost in each approach?

2. **Who can reschedule?** Only the host? The invitee? Both?
   - Design the authorization check in the handler

3. **What validations apply to the new time?**
   - New start must be in the future
   - New time must not conflict with existing bookings
   - How do you write this without duplicating `CreateBookingHandler` logic?

4. **Write the domain method signature:**
   ```csharp
   public Result Reschedule(DateTimeOffset newStart, DateTimeOffset newEnd, /* what else? */)
   ```
   What parameters does the aggregate need? What does it validate internally vs. what should the handler validate?

---

## Kata 5: Refactor `GetAvailableSlotsHandler` for Testability

**Current problem:**  
`GetAvailableSlotsHandler` is a complex handler that mixes orchestration (load data, coordinate objects) with computation (generate slots, filter conflicts). Unit testing the slot generation logic requires mocking repositories.

**Your task:**  
Extract the slot generation logic into a pure domain service or static method that can be tested without any I/O.

**Step 1:** Identify what parts of the handler are pure computation (take data, return data, no I/O) vs. orchestration (database calls, service calls).

**Step 2:** Design a new method or class:
```csharp
// Illustrative target
public static class SlotCalculator
{
    public static IReadOnlyList<TimeSlot> GetAvailableSlots(
        Schedule schedule,
        IReadOnlyList<BookingConflict> existingBookings,
        DateOnly date,
        int durationMinutes,
        TimeZoneInfo timezone)
    { ... }
}
```

**Step 3:** Write two unit tests for the extracted method:
- Empty existing bookings → all slots available
- Existing booking 9:00–10:00 → 9:00 slot not available, 10:00 slot is

**What you gain:** Handler tests become trivial (mock returns known data, assert slot calculator is called). Slot calculation tests are fast (no I/O).

---

## Kata 6: Event Sourcing — Would It Help Here?

**Scenario:** A colleague suggests rewriting the booking system using Event Sourcing. Instead of storing the current state of a booking (status, cancellationReason, etc.), store the events themselves:

```
BookingCreated { ... }
BookingCancelled { reason: "Changed plans" }
BookingRescheduled { newStart: ..., newEnd: ... }
```

The current state of a booking is reconstructed by replaying events from the beginning.

**Your task — argue both sides:**

**For Event Sourcing:**
- What features become easier? (Audit trail, undo, debugging)
- Which of this repo's current risks would Event Sourcing solve?

**Against Event Sourcing:**
- What gets harder? (Simple reads, queries, schema evolution)
- How complex would `GetAvailableSlotsHandler` become?
- What is the operational overhead?

**Verdict:** Write a 2-paragraph recommendation, as if presenting to your team, on whether to adopt Event Sourcing for this codebase at its current stage.
