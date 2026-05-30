using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class MfaBackupCodeConfiguration : IEntityTypeConfiguration<MfaBackupCode>
{
    public void Configure(EntityTypeBuilder<MfaBackupCode> builder)
    {
        builder.ToTable("SEC_MfaBackupCodes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CodeHash).HasMaxLength(120).IsRequired();
        builder.Property(e => e.BatchId).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.BatchId })
            .HasDatabaseName("IX_SEC_MfaBackupCodes_User_Batch");

        builder.HasOne(e => e.User)
            .WithMany(u => u.MfaBackupCodes)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
