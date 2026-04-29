using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CertificateEntryConfiguration : IEntityTypeConfiguration<CertificateEntry>
{
    public void Configure(EntityTypeBuilder<CertificateEntry> builder)
    {
        builder.ToTable("CDT_CertificateEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.EntryType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.PreviousRate).HasPrecision(12, 3);
        builder.Property(e => e.CurrentRate).HasPrecision(12, 3);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Description).HasMaxLength(200);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
