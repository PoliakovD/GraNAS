using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Signaling.DAL.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("table_devices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasColumnName("user_id");
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.DeviceName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("device_name");

        builder.Property(x => x.Platform)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.HasIndex(x => new { x.UserId, x.DeviceName }).IsUnique();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.LastSeenAt)
            .HasColumnName("last_seen_at")
            .HasDefaultValueSql("NOW()");
    }
}
