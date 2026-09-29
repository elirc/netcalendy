using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Configurations;

public sealed class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable("event_types");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => EventTypeId.From(v))
            .HasColumnName("id");

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.OwnerId)
            .HasConversion(id => id.Value, v => UserId.From(v))
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Slug).HasMaxLength(63).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Color).HasMaxLength(7).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.OwnsOne(e => e.Duration, d =>
        {
            d.Property(x => x.Minutes).HasColumnName("duration_minutes").IsRequired();
        });

        builder.HasIndex(e => new { e.OwnerId, e.Slug, e.TenantId }).IsUnique()
            .HasFilter("is_active = true");
    }
}
