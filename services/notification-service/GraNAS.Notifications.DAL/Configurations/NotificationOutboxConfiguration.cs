using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Notifications.DAL.Configurations;

public class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationOutbox> builder)
    {
        builder.ToTable("table_notification_outbox");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.NotificationEventId).HasColumnName("notification_event_id").IsRequired();
        builder.Property(o => o.Target).HasColumnName("target")
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(o => o.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(16).IsRequired()
            .HasDefaultValue(OutboxStatus.Pending);
        builder.Property(o => o.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(o => o.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(o => o.LastError).HasColumnName("last_error").HasMaxLength(2048);
        builder.Property(o => o.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(o => o.NotificationEvent)
            .WithMany()
            .HasForeignKey(o => o.NotificationEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.Target, o.Status, o.NextAttemptAt })
            .HasDatabaseName("ix_notification_outbox_delivery");
    }
}
