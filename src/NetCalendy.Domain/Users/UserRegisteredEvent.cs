using NetCalendy.Domain.Common;
using NetCalendy.Domain.Tenants;

namespace NetCalendy.Domain.Users;

public sealed record UserRegisteredEvent(UserId UserId, TenantId TenantId, string Email) : DomainEvent;
