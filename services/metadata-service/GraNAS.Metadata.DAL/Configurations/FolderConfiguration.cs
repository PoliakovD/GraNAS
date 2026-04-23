using GraNAS.Metadata.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Metadata.DAL.Configurations;

public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
  public void Configure(EntityTypeBuilder<Folder> builder)
  {
    builder.ToTable("table_folders");

    builder.HasKey(f => f.Id);
    builder.Property(f => f.Id)
      .ValueGeneratedOnAdd();

    builder.Property(f => f.OwnerId)
      .IsRequired()
      .HasColumnName("owner_id");

    builder.HasIndex(f => f.OwnerId, "IX_folders_owner_id");

    builder.Property(f => f.Name)
      .IsRequired()
      .HasMaxLength(255)
      .HasColumnName("name");

    builder.Property(f => f.CreatedAt)
      .HasColumnName("created_at")
      .HasDefaultValueSql("NOW()")
      .ValueGeneratedOnAdd();

    builder.Property(f => f.UpdatedAt)
      .HasColumnName("updated_at");
  }
}
