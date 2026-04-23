using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using File = GraNAS.Metadata.Models.File;

namespace GraNAS.Metadata.DAL.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<File>
{
  public void Configure(EntityTypeBuilder<File> builder)
  {
    builder.ToTable("table_files");

    builder.HasKey(f => f.Id);
    builder.Property(f => f.Id)
      .ValueGeneratedOnAdd();

    builder.Property(f => f.FolderId)
      .IsRequired()
      .HasColumnName("folder_id");

    builder.HasIndex(f => f.FolderId, "IX_files_folder_id");

    builder.Property(f => f.OwnerId)
      .IsRequired()
      .HasColumnName("owner_id");

    builder.HasIndex(f => f.OwnerId, "IX_files_owner_id");

    builder.Property(f => f.Name)
      .IsRequired()
      .HasMaxLength(255)
      .HasColumnName("name");

    builder.Property(f => f.Type)
      .IsRequired()
      .HasMaxLength(100)
      .HasColumnName("type");

    builder.Property(f => f.CreatedAt)
      .HasColumnName("created_at")
      .HasDefaultValueSql("NOW()")
      .ValueGeneratedOnAdd();

    builder.Property(f => f.UpdatedAt)
      .HasColumnName("updated_at");

    builder.HasOne(f => f.Folder)
      .WithMany(folder => folder.Files)
      .HasForeignKey(f => f.FolderId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
