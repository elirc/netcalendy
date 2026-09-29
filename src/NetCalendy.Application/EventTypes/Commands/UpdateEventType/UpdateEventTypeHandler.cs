using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.EventTypes.Commands.UpdateEventType;

public sealed class UpdateEventTypeHandler(
    IEventTypeRepository eventTypes,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<UpdateEventTypeCommand, Result>
{
    public async Task<Result> Handle(UpdateEventTypeCommand cmd, CancellationToken ct)
    {
        var et = await eventTypes.GetByIdAsync(EventTypeId.From(cmd.EventTypeId), ct);
        if (et is null) return Result.Failure(Error.NotFound("EventType", cmd.EventTypeId));

        if (et.OwnerId != UserId.From(currentUser.UserId!.Value))
            return Result.Failure(Error.Validation("EventType.Forbidden", "You do not own this event type."));

        var result = et.Update(cmd.Name, cmd.DurationMinutes, cmd.Description, cmd.Color);
        if (result.IsFailure) return result;

        eventTypes.Update(et);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
