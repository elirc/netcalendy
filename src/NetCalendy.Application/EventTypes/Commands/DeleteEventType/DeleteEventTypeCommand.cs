using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.EventTypes.Commands.DeleteEventType;

public sealed record DeleteEventTypeCommand(Guid EventTypeId) : IRequest<Result>;
