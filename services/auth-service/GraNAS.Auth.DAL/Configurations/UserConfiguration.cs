using GraNAS.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Auth.DAL.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
  public void Configure(EntityTypeBuilder<User> builder)
  {
    builder.ToTable("table_users");

    builder.HasKey(u => u.Id);
    builder.Property(u => u.Id)
      .ValueGeneratedOnAdd();

    builder.Property(u => u.Email)
      .IsRequired()
      .HasMaxLength(255);

    builder.HasIndex(u => u.Email)
      .IsUnique();

    builder.Property(u => u.PasswordHash)
      .IsRequired()
      .HasColumnName("password_hash");

    builder.Property(u => u.IsAdmin)
      .HasColumnName("is_admin");

    builder.Property(u => u.CreatedAt)
      .HasColumnName("created_at")
      .HasDefaultValueSql("NOW()")
      .ValueGeneratedOnAdd();

    builder.HasMany(u => u.RefreshTokens)
      .WithOne(rt => rt.User)
      .HasForeignKey(rt => rt.UserId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
