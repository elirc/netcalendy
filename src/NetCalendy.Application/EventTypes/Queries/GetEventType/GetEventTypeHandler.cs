using MediatR;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;

namespace NetCalendy.Application.EventTypes.Queries.GetEventType;

public sealed class GetEventTypeHandler(IEventTypeRepository eventTypes)
    : IRequestHandler<GetEventTypeQuery, Result<EventTypeDto>>
{
    public async Task<Result<EventTypeDto>> Handle(GetEventTypeQuery query, CancellationToken ct)
    {
        var et = await eventTypes.GetByIdAsync(EventTypeId.From(query.EventTypeId), ct);
        if (et is null) return Result.Failure<EventTypeDto>(Error.NotFound("EventType", query.EventTypeId));

        return Result.Success(new EventTypeDto(
            et.Id.Value, et.OwnerId.Value, et.Name, et.Slug,
            et.Duration.Minutes, et.Description, et.Color, et.IsActive));
    }
}
