using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Schedules.Queries.GetAvailableSlots;

public sealed record GetAvailableSlotsQuery(
    Guid EventTypeId,
    Guid HostUserId,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<Result<IReadOnlyList<AvailableSlotDto>>>;

public sealed record AvailableSlotDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string DisplayTime);
