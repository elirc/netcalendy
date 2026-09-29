using NetCalendy.Application.Common;

namespace NetCalendy.Application.Tests.Common;

public sealed class MockCurrentTenantService(Guid tenantId) : ICurrentTenantService
{
    public Guid? TenantId => tenantId;
    public string? TenantSlug => "test-tenant";
}
