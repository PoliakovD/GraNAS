using GraNAS.Models;
using Microsoft.EntityFrameworkCore;
using File = GraNAS.Models.File;

namespace GraNAS.WebAPI.DAL;

public class AppDbContext: DbContext
{
  public DbSet<User> Users { get; set; }
  public DbSet<RefreshToken> RefreshTokens { get; set; }
  public DbSet<Folder> Folders { get; set; }
  public DbSet<File> Files { get; set; }
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {

  }
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Дефолтные значения для CreatedAt
    modelBuilder.Entity<Folder>()
      .Property(f => f.CreatedAt)
      .HasDefaultValueSql("NOW()");

    modelBuilder.Entity<File>()
      .Property(f => f.CreatedAt)
      .HasDefaultValueSql("NOW()");

    // Каскадное удаление: при удалении пользователя удаляются все его папки и файлы
    modelBuilder.Entity<Folder>()
      .HasOne(f => f.Owner)
      .WithMany()
      .HasForeignKey(f => f.OwnerId)
      .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<File>()
      .HasOne(f => f.Owner)
      .WithMany()
      .HasForeignKey(f => f.OwnerId)
      .OnDelete(DeleteBehavior.Cascade);

    // При удалении папки удаляются все её файлы
    modelBuilder.Entity<File>()
      .HasOne(f => f.Folder)
      .WithMany(f => f.Files)
      .HasForeignKey(f => f.FolderId)
      .OnDelete(DeleteBehavior.Cascade);

  }
}
