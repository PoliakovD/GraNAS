using GraNAS.Signaling.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Signaling.DAL.Configurations;

public class DeviceFolderConfiguration : IEntityTypeConfiguration<DeviceFolder>
{
    public void Configure(EntityTypeBuilder<DeviceFolder> builder)
    {
        builder.ToTable("table_device_folders");

        builder.HasKey(df => df.FolderId);

        builder.Property(df => df.FolderId)
            .HasColumnName("folder_id")
            .ValueGeneratedNever();

        builder.Property(df => df.DeviceId)
            .IsRequired()
            .HasColumnName("device_id");

        builder.Property(df => df.ClaimedAt)
            .HasColumnName("claimed_at")
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.HasOne(df => df.Device)
            .WithMany()
            .HasForeignKey(df => df.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(df => df.DeviceId)
            .HasDatabaseName("IX_device_folders_device_id");
    }
}
