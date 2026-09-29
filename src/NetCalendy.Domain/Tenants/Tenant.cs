using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.Tenants;

public sealed class Tenant : AggregateRoot<TenantId>
{
    private Tenant(TenantId id, string name, string slug) : base(id)
    {
        Name = name;
        Slug = slug;
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public string Name { get; private set; }
    public string Slug { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static Result<Tenant> Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Tenant>(Error.Validation("Tenant.InvalidName", "Tenant name cannot be empty."));

        if (string.IsNullOrWhiteSpace(slug) || !IsValidSlug(slug))
            return Result.Failure<Tenant>(Error.Validation("Tenant.InvalidSlug", "Slug must be lowercase alphanumeric with hyphens only."));

        var tenant = new Tenant(TenantId.New(), name, slug.ToLowerInvariant());
        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id));
        return Result.Success(tenant);
    }

    public void Deactivate() => IsActive = false;

    private static bool IsValidSlug(string slug) =>
        slug.Length <= 63 && System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$");
}
