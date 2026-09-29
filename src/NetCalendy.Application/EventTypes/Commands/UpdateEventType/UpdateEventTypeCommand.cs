using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.EventTypes.Commands.UpdateEventType;

public sealed record UpdateEventTypeCommand(
    Guid EventTypeId,
    string Name,
    int DurationMinutes,
    string? Description,
    string Color) : IRequest<Result>;
