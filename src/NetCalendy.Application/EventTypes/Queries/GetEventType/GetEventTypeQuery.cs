using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.EventTypes.Queries.GetEventType;

public sealed record GetEventTypeQuery(Guid EventTypeId) : IRequest<Result<EventTypeDto>>;

public sealed record EventTypeDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    string Slug,
    int DurationMinutes,
    string? Description,
    string Color,
    bool IsActive);
