using Microsoft.EntityFrameworkCore;

namespace GraNAS.Signaling.DAL;

public class SignalingDbContext : DbContext
{
    public DbSet<GraNAS.Signaling.Models.Device> Devices => Set<GraNAS.Signaling.Models.Device>();

    public SignalingDbContext(DbContextOptions<SignalingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SignalingDbContext).Assembly);
    }
}
