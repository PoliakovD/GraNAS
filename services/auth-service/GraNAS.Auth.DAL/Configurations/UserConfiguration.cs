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

    builder.Property(u => u.Avatar)
      .HasColumnName("avatar")
      .IsRequired(false);

    builder.Property(u => u.AvatarContentType)
      .HasColumnName("avatar_content_type")
      .HasMaxLength(64)
      .IsRequired(false);

    builder.Property(u => u.AvatarUpdatedAt)
      .HasColumnName("avatar_updated_at")
      .IsRequired(false);

    builder.HasMany(u => u.RefreshTokens)
      .WithOne(rt => rt.User)
      .HasForeignKey(rt => rt.UserId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
