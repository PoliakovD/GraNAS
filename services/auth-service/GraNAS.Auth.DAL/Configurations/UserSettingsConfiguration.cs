using GraNAS.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Auth.DAL.Configurations;

public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("table_user_settings");

        builder.HasKey(s => s.UserId);
        builder.Property(s => s.UserId).HasColumnName("user_id").ValueGeneratedNever();

        builder.Property(s => s.NotificationPrefsJson)
            .HasColumnName("notification_prefs")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
