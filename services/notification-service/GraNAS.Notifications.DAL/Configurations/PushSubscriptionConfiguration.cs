using GraNAS.Notifications.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Notifications.DAL.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("table_push_subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.Endpoint).HasColumnName("endpoint").IsRequired();
        builder.Property(s => s.P256dh).HasColumnName("p256dh").IsRequired();
        builder.Property(s => s.Auth).HasColumnName("auth").IsRequired();
        builder.Property(s => s.UserAgent).HasColumnName("user_agent");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(s => s.LastUsedAt).HasColumnName("last_used_at");

        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_push_subscriptions_user_id");
        builder.HasIndex(s => s.Endpoint).IsUnique().HasDatabaseName("ix_push_subscriptions_endpoint");
    }
}
