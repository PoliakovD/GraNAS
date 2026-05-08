using GraNAS.Sharing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraNAS.Sharing.DAL.Configurations;

public class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        builder.ToTable("table_share_links");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        builder.Property(s => s.FolderId)
            .IsRequired()
            .HasColumnName("folder_id");

        builder.Property(s => s.OwnerId)
            .IsRequired()
            .HasColumnName("owner_id");

        builder.Property(s => s.TokenHash)
            .IsRequired()
            .HasColumnName("token_hash")
            .HasMaxLength(64);

        builder.Property(s => s.TokenEncrypted)
            .IsRequired()
            .HasColumnName("token_encrypted")
            .HasMaxLength(512)
            .HasDefaultValue(string.Empty);

        builder.Property(s => s.Path)
            .HasColumnName("path")
            .HasMaxLength(1024);

        builder.Property(s => s.ExpiresAt)
            .IsRequired()
            .HasColumnName("expires_at");

        builder.Property(s => s.Revoked)
            .IsRequired()
            .HasColumnName("revoked")
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(s => s.TokenHash, "IX_share_links_token_hash").IsUnique();
        builder.HasIndex(s => s.FolderId, "IX_share_links_folder_id");
        builder.HasIndex(s => s.OwnerId, "IX_share_links_owner_id");
        builder.HasIndex(s => s.ExpiresAt, "IX_share_links_expires_at");
    }
}
