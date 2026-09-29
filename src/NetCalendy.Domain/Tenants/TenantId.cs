namespace NetCalendy.Domain.Tenants;

public record TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
