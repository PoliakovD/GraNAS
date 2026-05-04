using GraNAS.Notifications.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Notifications.DAL.Configurations;

public class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> builder)
    {
        builder.ToTable("table_notification_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Type).HasColumnName("type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
        builder.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(e => e.ReadAt).HasColumnName("read_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.EventId).IsUnique().HasDatabaseName("ix_notification_events_event_id");
        builder.HasIndex(e => new { e.UserId, e.CreatedAt }).HasDatabaseName("ix_notification_events_user_created");
        builder.HasIndex(e => new { e.UserId, e.IsRead }).HasDatabaseName("ix_notification_events_user_read");
    }
}
