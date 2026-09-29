using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.Tenants;

public sealed record TenantCreatedEvent(TenantId TenantId) : DomainEvent;
