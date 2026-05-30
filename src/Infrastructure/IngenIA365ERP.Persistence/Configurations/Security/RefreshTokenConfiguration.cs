using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("SEC_RefreshTokens");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Token).HasMaxLength(500).IsRequired();
        builder.Property(e => e.TokenHash).HasMaxLength(120);
        builder.Property(e => e.RevokedBy).HasMaxLength(100);
        builder.Property(e => e.ReplacedByToken).HasMaxLength(500);
        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.RevocationReason).HasMaxLength(40);

        builder.HasIndex(e => e.TokenHash).HasDatabaseName("IX_SEC_RefreshTokens_TokenHash");
        builder.HasIndex(e => e.FamilyId).HasDatabaseName("IX_SEC_RefreshTokens_FamilyId");

        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
