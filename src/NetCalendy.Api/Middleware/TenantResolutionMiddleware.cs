using NetCalendy.Domain.Repositories;

namespace NetCalendy.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantSlugHeader = "X-Tenant-Slug";

    public async Task InvokeAsync(HttpContext context, ITenantRepository tenants)
    {
        // Tenant resolution: check header first, then subdomain
        var slug = context.Request.Headers[TenantSlugHeader].FirstOrDefault()
            ?? ExtractSubdomain(context.Request.Host.Host);

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var tenant = await tenants.GetBySlugAsync(slug);
            if (tenant is not null)
            {
                context.Items["TenantId"] = tenant.Id.Value;
                context.Items["TenantSlug"] = tenant.Slug;
            }
        }

        await next(context);
    }

    private static string? ExtractSubdomain(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 3 ? parts[0] : null;
    }
}
