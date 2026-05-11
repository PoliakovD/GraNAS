using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Signaling.DAL.Configurations;

/// <summary>
/// Конфигурация маппинга сущности <see cref="Device"/> на таблицу <c>table_devices</c>.
/// Уникальный индекс по паре <c>(user_id, device_name)</c> запрещает одному пользователю
/// иметь два устройства с одинаковым именем. Платформа хранится как строка.
/// </summary>
public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    /// <inheritdoc/>
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
