using Microsoft.AspNetCore.Http;
using NetCalendy.Application.Common;
using System.Security.Claims;

namespace NetCalendy.Infrastructure.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
