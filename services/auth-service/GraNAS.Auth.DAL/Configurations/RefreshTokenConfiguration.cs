using GraNAS.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Auth.DAL.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
  public void Configure(EntityTypeBuilder<RefreshToken> builder)
  {
    builder.ToTable("table_refresh_tokens");

    builder.HasKey(rt => rt.Id);
    builder.Property(rt => rt.Id)
      .ValueGeneratedOnAdd();

    builder.Property(rt => rt.UserId)
      .IsRequired()
      .HasColumnName("user_id");

    builder.Property(rt => rt.Token)
      .IsRequired()
      .HasMaxLength(255)
      .HasColumnName("token");

    builder.HasIndex(rt => rt.Token)
      .IsUnique();

    builder.Property(rt => rt.Expires)
      .IsRequired()
      .HasColumnName("expires");

    builder.Property(rt => rt.Revoked)
      .HasColumnName("revoked");

    builder.Property(rt => rt.CreatedAt)
      .HasColumnName("created_at")
      .HasDefaultValueSql("NOW()")
      .ValueGeneratedOnAdd();
  }
}
