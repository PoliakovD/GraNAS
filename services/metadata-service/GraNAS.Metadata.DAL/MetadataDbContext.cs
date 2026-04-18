using GraNAS.Metadata.Models;
using Microsoft.EntityFrameworkCore;
using File = GraNAS.Metadata.Models.File;

namespace GraNAS.Metadata.DAL;

public class MetadataDbContext : DbContext
{
  public DbSet<Folder> Folders => Set<Folder>();
  public DbSet<File> Files => Set<File>();

  public MetadataDbContext(DbContextOptions<MetadataDbContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MetadataDbContext).Assembly);
  }
}
