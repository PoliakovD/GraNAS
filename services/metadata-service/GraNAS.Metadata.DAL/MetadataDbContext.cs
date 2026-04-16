using GraNAS.Models;
using Microsoft.EntityFrameworkCore;
using File = GraNAS.Models.File;

namespace GraNAS.Metadata.DAL;

public class MetadataDbContext : DbContext
{
  public DbSet<Folder> Folders { get; set; }
  public DbSet<File> Files { get; set; }

  public MetadataDbContext(DbContextOptions<MetadataDbContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Folder>()
      .Property(f => f.CreatedAt)
      .HasDefaultValueSql("NOW()");

    modelBuilder.Entity<File>()
      .Property(f => f.CreatedAt)
      .HasDefaultValueSql("NOW()");

    // При удалении папки удаляются все её файлы
    modelBuilder.Entity<File>()
      .HasOne(f => f.Folder)
      .WithMany(f => f.Files)
      .HasForeignKey(f => f.FolderId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
