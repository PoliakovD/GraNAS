using GraNAS.Metadata.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Metadata.DAL.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
  public void Configure(EntityTypeBuilder<Permission> builder)
  {
    builder.ToTable("table_permissions");

    builder.HasKey(p => p.Id);
    builder.Property(p => p.Id).ValueGeneratedOnAdd();

    builder.Property(p => p.FolderId)
      .IsRequired()
      .HasColumnName("folder_id");

    builder.Property(p => p.UserId)
      .IsRequired()
      .HasColumnName("user_id");

    builder.Property(p => p.AccessLevel)
      .IsRequired()
      .HasColumnName("access_level")
      .HasMaxLength(16)
      .HasConversion<string>();

    builder.Property(p => p.Path)
      .HasColumnName("path")
      .HasMaxLength(1024);

    builder.Property(p => p.CreatedAt)
      .HasColumnName("created_at")
      .HasDefaultValueSql("NOW()")
      .ValueGeneratedOnAdd();

    builder.Property(p => p.UpdatedAt)
      .HasColumnName("updated_at");

    builder.HasOne(p => p.Folder)
      .WithMany()
      .HasForeignKey(p => p.FolderId)
      .OnDelete(DeleteBehavior.Cascade);

    // Unique per (folder, user) — grant повторно = update
    builder.HasIndex(p => new { p.FolderId, p.UserId }, "IX_permissions_folder_user").IsUnique();
    builder.HasIndex(p => p.UserId, "IX_permissions_user_id");
  }
}
