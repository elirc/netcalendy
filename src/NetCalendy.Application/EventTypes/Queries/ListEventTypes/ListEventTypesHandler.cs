using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Application.EventTypes.Queries.GetEventType;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.EventTypes.Queries.ListEventTypes;

public sealed class ListEventTypesHandler(
    IEventTypeRepository eventTypes,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant)
    : IRequestHandler<ListEventTypesQuery, Result<IReadOnlyList<EventTypeDto>>>
{
    public async Task<Result<IReadOnlyList<EventTypeDto>>> Handle(ListEventTypesQuery query, CancellationToken ct)
    {
        var tenantId = TenantId.From(currentTenant.TenantId!.Value);
        var ownerId = UserId.From(query.OwnerId ?? currentUser.UserId!.Value);

        var list = await eventTypes.ListByOwnerAsync(ownerId, tenantId, ct);

        var dtos = list.Select(et => new EventTypeDto(
            et.Id.Value, et.OwnerId.Value, et.Name, et.Slug,
            et.Duration.Minutes, et.Description, et.Color, et.IsActive))
            .ToList();

        return Result.Success<IReadOnlyList<EventTypeDto>>(dtos);
    }
}
