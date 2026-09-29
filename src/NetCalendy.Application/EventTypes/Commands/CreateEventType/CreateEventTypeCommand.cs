using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.EventTypes.Commands.CreateEventType;

public sealed record CreateEventTypeCommand(
    string Name,
    string Slug,
    int DurationMinutes,
    string? Description,
    string Color = "#0069FF") : IRequest<Result<CreateEventTypeResponse>>;

public sealed record CreateEventTypeResponse(
    Guid EventTypeId,
    string Name,
    string Slug,
    int DurationMinutes,
    string Color);
