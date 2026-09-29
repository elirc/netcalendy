using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Configurations;

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("schedules");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, v => ScheduleId.From(v))
            .HasColumnName("id");

        builder.Property(s => s.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, v => UserId.From(v))
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.OwnsMany(s => s.Rules, rules =>
        {
            rules.ToTable("schedule_rules");
            rules.WithOwner().HasForeignKey("schedule_id");
            rules.Property<int>("id").ValueGeneratedOnAdd();
            rules.HasKey("id");

            rules.Property(r => r.DayOfWeek)
                .HasColumnName("day_of_week")
                .IsRequired();

            rules.OwnsOne(r => r.TimeRange, tr =>
            {
                tr.Property(x => x.Start).HasColumnName("start_time").IsRequired();
                tr.Property(x => x.End).HasColumnName("end_time").IsRequired();
            });
        });
    }
}
