using GraNAS.Metadata.Models;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Metadata.DAL;

public class MetadataDbContext : DbContext
{
  public DbSet<Folder> Folders => Set<Folder>();
  public DbSet<Permission> Permissions => Set<Permission>();

  public MetadataDbContext(DbContextOptions<MetadataDbContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MetadataDbContext).Assembly);
  }
}
