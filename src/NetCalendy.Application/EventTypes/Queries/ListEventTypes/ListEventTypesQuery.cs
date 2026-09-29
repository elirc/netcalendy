using MediatR;
using NetCalendy.Application.EventTypes.Queries.GetEventType;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.EventTypes.Queries.ListEventTypes;

public sealed record ListEventTypesQuery(Guid? OwnerId = null) : IRequest<Result<IReadOnlyList<EventTypeDto>>>;
