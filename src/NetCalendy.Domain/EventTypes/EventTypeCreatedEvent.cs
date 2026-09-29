using NetCalendy.Domain.Common;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.EventTypes;

public sealed record EventTypeCreatedEvent(EventTypeId EventTypeId, TenantId TenantId, UserId OwnerId) : DomainEvent;
