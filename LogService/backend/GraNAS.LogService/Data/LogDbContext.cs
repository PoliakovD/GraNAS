using GraNAS.LogService.Models;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.LogService.Data;

public class LogDbContext : DbContext
{
  public LogDbContext(DbContextOptions<LogDbContext> options) : base(options) { }
  public DbSet<LogEntry> Logs { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<LogEntry>()
      .HasIndex(e => e.Timestamp);
    modelBuilder.Entity<LogEntry>()
      .HasIndex(e => e.Service);
    modelBuilder.Entity<LogEntry>()
      .HasIndex(e => e.Level);
    modelBuilder.Entity<LogEntry>()
      .HasIndex(e => e.CorrelationId);
  }
}
