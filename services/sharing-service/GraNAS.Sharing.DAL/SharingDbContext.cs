using GraNAS.Sharing.Models;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Sharing.DAL;

public class SharingDbContext : DbContext
{
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    public SharingDbContext(DbContextOptions<SharingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SharingDbContext).Assembly);
    }
}
