using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.Schedules.Queries.GetAvailableSlots;

public sealed class GetAvailableSlotsHandler(
    IEventTypeRepository eventTypes,
    IScheduleRepository schedules,
    IBookingRepository bookings,
    ICurrentTenantService currentTenant)
    : IRequestHandler<GetAvailableSlotsQuery, Result<IReadOnlyList<AvailableSlotDto>>>
{
    public async Task<Result<IReadOnlyList<AvailableSlotDto>>> Handle(GetAvailableSlotsQuery query, CancellationToken ct)
    {
        var tenantId = TenantId.From(currentTenant.TenantId!.Value);
        var hostId = UserId.From(query.HostUserId);

        var eventType = await eventTypes.GetByIdAsync(EventTypeId.From(query.EventTypeId), ct);
        // Match booking's public resource scope before reading host schedules.
        if (eventType is null || eventType.TenantId != tenantId || eventType.OwnerId != hostId)
            return Result.Failure<IReadOnlyList<AvailableSlotDto>>(Error.NotFound("EventType", query.EventTypeId));
        if (!eventType.IsActive)
            return Result.Failure<IReadOnlyList<AvailableSlotDto>>(Error.Validation("EventType.Inactive", "This event type is not available."));

        var schedule = await schedules.GetDefaultByUserAsync(hostId, tenantId, ct);
        if (schedule is null) return Result.Success<IReadOnlyList<AvailableSlotDto>>([]);

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone); }
        catch { return Result.Failure<IReadOnlyList<AvailableSlotDto>>(Error.Validation("Schedule.InvalidTimezone", "Invalid timezone.")); }

        var rangeStart = new DateTimeOffset(query.StartDate.Year, query.StartDate.Month, query.StartDate.Day, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = new DateTimeOffset(query.EndDate.Year, query.EndDate.Month, query.EndDate.Day, 23, 59, 59, TimeSpan.Zero);

        var existingBookings = await bookings.ListByHostAsync(hostId, tenantId, rangeStart, rangeEnd, ct);

        var slots = new List<AvailableSlotDto>();
        for (var date = query.StartDate; date <= query.EndDate; date = date.AddDays(1))
        {
            foreach (var slot in schedule.GetAvailableSlots(date, eventType.Duration.Minutes))
            {
                var startUtc = slot.ToUtc(tz);
                var endUtc = startUtc.AddMinutes(eventType.Duration.Minutes);

                if (startUtc < DateTimeOffset.UtcNow) continue;

                var hasConflict = existingBookings.Any(b => b.ConflictsWith(startUtc, endUtc));
                if (hasConflict) continue;

                slots.Add(new AvailableSlotDto(startUtc, endUtc, startUtc.ToString("hh:mm tt")));
            }
        }

        return Result.Success<IReadOnlyList<AvailableSlotDto>>(slots);
    }
}
