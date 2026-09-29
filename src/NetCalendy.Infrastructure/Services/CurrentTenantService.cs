using Microsoft.AspNetCore.Http;
using NetCalendy.Application.Common;

namespace NetCalendy.Infrastructure.Services;

public sealed class CurrentTenantService(IHttpContextAccessor httpContextAccessor) : ICurrentTenantService
{
    public Guid? TenantId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst("tenant_id");
            return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public string? TenantSlug =>
        httpContextAccessor.HttpContext?.User.FindFirst("tenant_slug")?.Value;
}
