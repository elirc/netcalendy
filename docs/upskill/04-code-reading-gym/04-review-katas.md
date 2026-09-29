# 04 — Review Katas

Each kata presents a fictional PR. Your job: identify blocking issues, important improvements, and optional suggestions — in that order of priority. Then compare against the expected findings.

---

## Kata 1: Add "Buffer Time" Between Bookings

**Author intent:** Add a `bufferMinutes` field to `EventType` so the host has recovery time between meetings.

**Fake diff summary:**
```diff
// EventType.cs
+ public int BufferMinutes { get; private set; } = 0;

// EventType.Create()
- var et = new EventType(EventTypeId.New(), tenantId, ownerId, name, slug, durationResult.Value, description, color);
+ var et = new EventType(EventTypeId.New(), tenantId, ownerId, name, slug, durationResult.Value, description, color, bufferMinutes);

// GetAvailableSlotsHandler.cs
- foreach (var slot in schedule.GetAvailableSlots(date, eventType.Duration.Minutes))
+ foreach (var slot in schedule.GetAvailableSlots(date, eventType.Duration.Minutes + eventType.BufferMinutes))
```

**Resembles:** [`src/NetCalendy.Domain/EventTypes/EventType.cs`](../../src/NetCalendy.Domain/EventTypes/EventType.cs), [`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)

**Expected findings:**

🚫 **Blocking:**
- Buffer time should not inflate the slot duration. If a 30-min meeting has 10-min buffer, the slot is 30 mins but the next available slot starts 40 mins later. The current change makes slots 40 mins long — the invitee now books 40 minutes instead of 30.
- No EF migration added — `BufferMinutes` won't persist.
- No validator update — `bufferMinutes` can be negative.

⚠️ **Important:**
- `BufferMinutes` as a plain `int` on the entity vs. a `Duration` value object — inconsistent with `Duration` pattern.
- `GetAvailableSlots` in the Schedule domain method should accept `effectiveDuration` (meeting + buffer) but the returned `TimeSlot.End` should reflect only meeting duration, not buffer. The buffer affects when the *next* slot starts, not when *this* booking ends.

💡 **Optional:**
- Consider whether buffer is per-event-type or per-schedule.

**Good review comment example:**
> The `Duration.Minutes + BufferMinutes` change inflates the actual booking duration. A 30-min meeting with a 10-min buffer should still create a `Booking` from `T` to `T+30`, but block the host from `T` to `T+40`. The slot generation needs two concepts: meeting duration (for the booking) and step size (for advancing the clock). Consider passing both to `GetAvailableSlots`.

---

## Kata 2: Add User Profile Update Endpoint

**Author intent:** Let authenticated users update their name and timezone.

**Fake diff summary:**
```diff
// UsersController.cs (new file)
+ [ApiController, Route("api/users"), Authorize]
+ public class UsersController(IMediator mediator) : ControllerBase
+ {
+     [HttpPut("me")]
+     public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
+     {
+         var userId = Guid.Parse(User.FindFirst("sub")!.Value);
+         var result = await mediator.Send(new UpdateProfileCommand(userId, request.Name, request.Timezone), ct);
+         return result.IsSuccess ? Ok() : BadRequest(result.Error);
+     }
+ }
```

**Resembles:** [`src/NetCalendy.Api/Controllers/EventTypesController.cs`](../../src/NetCalendy.Api/Controllers/EventTypesController.cs)

**Expected findings:**

🚫 **Blocking:**
- `BadRequest(result.Error)` — should use `result.ToActionResult()` from `ResultExtensions` for consistent error shape. Returning the raw `Error` record as JSON exposes internal error codes directly.
- `Guid.Parse(...)` throws if the claim is missing or malformed — use `Guid.TryParse` or the `ICurrentUserService` abstraction instead of reaching into `User.FindFirst` directly in the controller.

⚠️ **Important:**
- No validator for `UpdateProfileCommand` — name could be empty string.
- No test for the new endpoint.
- `UpdateProfileCommand` handler presumably loads the user by ID — does it check tenant scope to ensure the user belongs to the calling tenant?

**Good review comment example:**
> Use `ICurrentUserService` instead of parsing the claim directly in the controller. The service is already injected and tested. If we ever change the claim name (e.g., from `"sub"` to `"user_id"`), we'll need to change this in one place, not hunt across controllers.

---

## Kata 3: Add Available Slots Pagination

**Author intent:** Add `page` and `pageSize` query params to `GET /api/schedules/available-slots`.

**Fake diff summary:**
```diff
// GetAvailableSlotsQuery.cs
+ int Page = 1,
+ int PageSize = 20

// GetAvailableSlotsHandler.cs (after slots are computed)
+ return Result.Success<IReadOnlyList<AvailableSlotDto>>(
+     slots.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList()
+ );
```

**Resembles:** [`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)

**Expected findings:**

🚫 **Blocking:**
- The pagination is applied **after** loading all bookings and computing all slots in memory. For a user with many bookings and a 60-day range, the full computation still runs — pagination only affects the response size, not the work done. This is "application-level pagination" and doesn't solve the performance problem.
- No validation: `pageSize` could be 10000.
- No total count returned — the client can't build a pagination UI.

⚠️ **Important:**
- Return a paginated response type: `{ items: [...], totalCount: N, page: N, pageSize: N }` — not just the raw list.

💡 **Optional:**
- Consider cursor-based pagination (by `startUtc`) instead of offset-based — more stable when data changes between pages.

---

## Kata 4: Add Booking Notification Email

**Author intent:** Send a confirmation email to the invitee after a booking is created.

**Fake diff summary:**
```diff
// CreateBookingHandler.cs
+ private readonly IEmailService _emailService;
+
+ // After uow.SaveChangesAsync(ct):
+ await _emailService.SendBookingConfirmation(
+     bookingResult.Value.Invitee.Email,
+     bookingResult.Value.StartTime);
```

**Resembles:** [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)

**Expected findings:**

🚫 **Blocking:**
- The email send happens synchronously in the HTTP handler. A slow email service (network latency, provider outage) blocks the booking response. If the email service times out, the user gets a 500 even though the booking was saved.
- No retry/durability: if the process crashes after save but before send, the email is lost.

⚠️ **Important:**
- The correct pattern is a domain event handler (`INotificationHandler<BookingCreatedEvent>`) or an outbox. The booking creation itself should not depend on email delivery.
- `IEmailService` would be a new interface — where is it defined? Application layer (correct) or Infrastructure?

**Good review comment example:**
> Sending email synchronously in the command handler couples booking success to email delivery. Consider implementing `INotificationHandler<BookingCreatedEvent>` instead — it runs after the save and can be moved to a background job when we're ready for durability. For now, at minimum, wrap the send in a try-catch so a failed email doesn't fail the booking.

---

## Kata 5: New Route: Public Event Type by Slug

**Author intent:** Fix the broken public booking flow by adding `GET /api/public/{userSlug}/{eventSlug}`.

**Fake diff summary:**
```diff
// PublicController.cs (new file)
+ [ApiController, Route("api/public")]
+ public class PublicController(IMediator mediator) : ControllerBase
+ {
+     [HttpGet("{userSlug}/{eventSlug}"), AllowAnonymous]
+     public async Task<IActionResult> GetEventType(string userSlug, string eventSlug, CancellationToken ct)
+     {
+         var result = await mediator.Send(new GetPublicEventTypeQuery(userSlug, eventSlug), ct);
+         return result.ToActionResult();
+     }
+ }
+
// GetPublicEventTypeQuery.cs (new)
+ public record GetPublicEventTypeQuery(string UserSlug, string EventSlug) : IRequest<Result<EventTypeDto>>;
+
// GetPublicEventTypeHandler.cs (new)
+ var user = await users.GetBySlugAsync(userSlug, tenantId); // IUserRepository has no GetBySlugAsync!
```

**Resembles:** [`src/NetCalendy.Api/Controllers/BookingsController.cs`](../../src/NetCalendy.Api/Controllers/BookingsController.cs)

**Expected findings:**

🚫 **Blocking:**
- `IUserRepository` has no `GetBySlugAsync` — users don't have a `slug` field. The query parameter is `userSlug` but users are identified by email and ID. The handler references a method that doesn't exist.
- The `tenantId` context is unclear for a fully public endpoint — if the URL doesn't include a tenant, how does the handler scope the query?

⚠️ **Important:**
- Users need a `slug` or `username` field (a new domain concept) before this endpoint can work.
- The query handler should not expose `IUserRepository` to search by a concept that doesn't exist in the domain.
- The `EventTypeDto` should be a reduced public DTO (no owner ID exposure).

💡 **Optional:**
- Consider whether `userSlug` + `eventSlug` is the right URL shape. Calendly uses `/{username}/{event-slug}`.

---

## Kata 6: Add Timezone to EventType

**Author intent:** Let each event type specify its own timezone instead of using the schedule's timezone.

**Fake diff summary:**
```diff
// EventType.cs
+ public string Timezone { get; private set; }

// EventTypeConfiguration.cs
+ builder.Property(e => e.Timezone).HasMaxLength(100).IsRequired();

// GetAvailableSlotsHandler.cs
- TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone);
+ TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(eventType.Timezone);
```

**Expected findings:**

⚠️ **Important:**
- No migration for the new non-nullable `Timezone` column — existing rows will fail with NOT NULL constraint violation.
- The new column requires a database default for migration safety: `builder.Property(...).HasDefaultValue("UTC")`.
- No validator for the new timezone string field — same cross-platform issue as the existing timezone fields (RISK-002).

💡 **Optional:**
- Consider whether event type timezone overriding schedule timezone is the right model. This adds ambiguity: does the host's schedule already control availability in the right timezone?
