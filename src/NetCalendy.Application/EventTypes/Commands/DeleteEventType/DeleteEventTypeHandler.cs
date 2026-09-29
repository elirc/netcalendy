using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.EventTypes.Commands.DeleteEventType;

public sealed class DeleteEventTypeHandler(
    IEventTypeRepository eventTypes,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<DeleteEventTypeCommand, Result>
{
    public async Task<Result> Handle(DeleteEventTypeCommand cmd, CancellationToken ct)
    {
        var et = await eventTypes.GetByIdAsync(EventTypeId.From(cmd.EventTypeId), ct);
        if (et is null) return Result.Failure(Error.NotFound("EventType", cmd.EventTypeId));

        if (et.OwnerId != UserId.From(currentUser.UserId!.Value))
            return Result.Failure(Error.Validation("EventType.Forbidden", "You do not own this event type."));

        et.Deactivate();
        eventTypes.Update(et);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
