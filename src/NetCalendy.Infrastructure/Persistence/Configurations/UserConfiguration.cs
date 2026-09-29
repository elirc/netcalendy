using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, v => UserId.From(v))
            .HasColumnName("id");

        builder.Property(u => u.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(254)
                .IsRequired();
        });

        builder.Property(u => u.Name).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex("TenantId", "Email_Value").HasDatabaseName("ix_users_tenant_email");

        builder.HasQueryFilter(u => u.IsActive);
    }
}
