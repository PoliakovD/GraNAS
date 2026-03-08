using GraNAS.Models;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.WebAPI.DAL;

public class AppDbContext: DbContext
{
  public DbSet<User> Users { get; set; }
  public DbSet<RefreshToken> RefreshTokens { get; set; }
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {

  }
}
