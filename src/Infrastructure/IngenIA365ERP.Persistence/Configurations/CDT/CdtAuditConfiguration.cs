using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CdtAuditConfiguration : IEntityTypeConfiguration<CdtAudit>
{
    public void Configure(EntityTypeBuilder<CdtAudit> builder)
    {
        builder.ToTable("CDT_Audit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Action).HasMaxLength(1).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(50);
        builder.Property(e => e.EntityType).HasMaxLength(50);
        builder.Property(e => e.LegalRepIdOld).HasMaxLength(20);
        builder.Property(e => e.LegalRepIdNew).HasMaxLength(20);
        builder.Property(e => e.LegalRepNameOld).HasMaxLength(50);
        builder.Property(e => e.LegalRepNameNew).HasMaxLength(50);
        builder.Property(e => e.AddressOld).HasMaxLength(50);
        builder.Property(e => e.AddressNew).HasMaxLength(50);
        builder.Property(e => e.PhoneOld).HasMaxLength(20);
        builder.Property(e => e.PhoneNew).HasMaxLength(20);
        builder.Property(e => e.MobileOld).HasMaxLength(20);
        builder.Property(e => e.MobileNew).HasMaxLength(20);
        builder.Property(e => e.StatusOld).HasMaxLength(2);
        builder.Property(e => e.StatusNew).HasMaxLength(2);
        builder.Property(e => e.InterestRateOld).HasPrecision(6, 3);
        builder.Property(e => e.InterestRateNew).HasPrecision(6, 3);
        builder.Property(e => e.AmountOld).HasPrecision(18, 2);
        builder.Property(e => e.AmountNew).HasPrecision(18, 2);
        builder.Property(e => e.CancelledByOld).HasMaxLength(20);
        builder.Property(e => e.CancelledByNew).HasMaxLength(20);
        builder.Property(e => e.IsCapitalizedOld).HasMaxLength(2);
        builder.Property(e => e.IsCapitalizedNew).HasMaxLength(2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
