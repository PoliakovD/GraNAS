using GraNAS.Models;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Auth.DAL;

public class AppDbContext : DbContext
{
  public DbSet<User> Users { get; set; }
  public DbSet<RefreshToken> RefreshTokens { get; set; }

  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
  }
}
