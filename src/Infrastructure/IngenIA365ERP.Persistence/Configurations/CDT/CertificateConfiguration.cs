using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("CDT_Certificates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.CertificateNumber).IsUnique();

        builder.Property(e => e.CertificateNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(10, 6);
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.RenewalType).HasMaxLength(2);
        builder.Property(e => e.PaymentMethod).HasMaxLength(2);
        builder.Property(e => e.LegalRepId).HasMaxLength(20);
        builder.Property(e => e.LegalRepName).HasMaxLength(50);
        builder.Property(e => e.BusinessAddress).HasMaxLength(50);
        builder.Property(e => e.Phone).HasMaxLength(20);
        builder.Property(e => e.Mobile).HasMaxLength(20);
        builder.Property(e => e.SignatoryId1).HasMaxLength(20);
        builder.Property(e => e.SignatoryId2).HasMaxLength(20);
        builder.Property(e => e.SignatoryId3).HasMaxLength(20);
        builder.Property(e => e.SignatoryName1).HasMaxLength(50);
        builder.Property(e => e.SignatoryName2).HasMaxLength(50);
        builder.Property(e => e.SignatoryName3).HasMaxLength(50);
        builder.Property(e => e.BeneficiaryId1).HasMaxLength(20);
        builder.Property(e => e.BeneficiaryId2).HasMaxLength(20);
        builder.Property(e => e.BeneficiaryId3).HasMaxLength(20);
        builder.Property(e => e.BeneficiaryId4).HasMaxLength(20);
        builder.Property(e => e.BeneficiaryId5).HasMaxLength(20);
        builder.Property(e => e.BeneficiaryName1).HasMaxLength(50);
        builder.Property(e => e.BeneficiaryName2).HasMaxLength(50);
        builder.Property(e => e.BeneficiaryName3).HasMaxLength(50);
        builder.Property(e => e.BeneficiaryName4).HasMaxLength(50);
        builder.Property(e => e.BeneficiaryName5).HasMaxLength(50);
        builder.Property(e => e.BeneficiaryPct1).HasPrecision(6, 3);
        builder.Property(e => e.BeneficiaryPct2).HasPrecision(6, 3);
        builder.Property(e => e.BeneficiaryPct3).HasPrecision(6, 3);
        builder.Property(e => e.BeneficiaryPct4).HasPrecision(6, 3);
        builder.Property(e => e.BeneficiaryPct5).HasPrecision(6, 3);
        builder.Property(e => e.CancelledByUserId).HasMaxLength(20);

        builder.HasMany(e => e.Entries).WithOne().HasForeignKey(e => e.CertificateId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
