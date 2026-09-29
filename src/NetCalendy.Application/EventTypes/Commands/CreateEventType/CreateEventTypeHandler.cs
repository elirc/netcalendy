using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.EventTypes.Commands.CreateEventType;

public sealed class CreateEventTypeHandler(
    IEventTypeRepository eventTypes,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IUnitOfWork uow)
    : IRequestHandler<CreateEventTypeCommand, Result<CreateEventTypeResponse>>
{
    public async Task<Result<CreateEventTypeResponse>> Handle(CreateEventTypeCommand cmd, CancellationToken ct)
    {
        var tenantId = TenantId.From(currentTenant.TenantId!.Value);
        var userId = UserId.From(currentUser.UserId!.Value);

        if (await eventTypes.SlugExistsForUserAsync(cmd.Slug, userId, tenantId, ct))
            return Result.Failure<CreateEventTypeResponse>(
                Error.Conflict("EventType.SlugTaken", $"You already have an event type with slug '{cmd.Slug}'."));

        var result = EventType.Create(tenantId, userId, cmd.Name, cmd.Slug, cmd.DurationMinutes, cmd.Description, cmd.Color);
        if (result.IsFailure) return Result.Failure<CreateEventTypeResponse>(result.Error);

        eventTypes.Add(result.Value);
        await uow.SaveChangesAsync(ct);

        var et = result.Value;
        return Result.Success(new CreateEventTypeResponse(et.Id.Value, et.Name, et.Slug, et.Duration.Minutes, et.Color));
    }
}
