using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, v => BookingId.From(v))
            .HasColumnName("id");

        builder.Property(b => b.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(b => b.EventTypeId)
            .HasConversion(id => id.Value, v => EventTypeId.From(v))
            .HasColumnName("event_type_id")
            .IsRequired();

        builder.Property(b => b.HostId)
            .HasConversion(id => id.Value, v => UserId.From(v))
            .HasColumnName("host_id")
            .IsRequired();

        builder.OwnsOne(b => b.Invitee, inv =>
        {
            inv.Property(x => x.Name).HasColumnName("invitee_name").HasMaxLength(150).IsRequired();
            inv.Property(x => x.Email).HasColumnName("invitee_email").HasMaxLength(254).IsRequired();
            inv.Property(x => x.Timezone).HasColumnName("invitee_timezone").HasMaxLength(100).IsRequired();
            inv.Property(x => x.Notes).HasColumnName("invitee_notes").HasMaxLength(1000);
        });

        builder.Property(b => b.StartTime).IsRequired();
        builder.Property(b => b.EndTime).IsRequired();
        builder.Property(b => b.Status).IsRequired();
        builder.Property(b => b.CancellationToken).HasMaxLength(16).IsRequired();
        builder.Property(b => b.CancellationReason).HasMaxLength(500);
        builder.Property(b => b.CreatedAt).IsRequired();

        builder.HasIndex(b => b.CancellationToken).IsUnique();
        builder.HasIndex(b => new { b.HostId, b.StartTime });
        builder.HasIndex(b => new { b.TenantId, b.StartTime });
    }
}
