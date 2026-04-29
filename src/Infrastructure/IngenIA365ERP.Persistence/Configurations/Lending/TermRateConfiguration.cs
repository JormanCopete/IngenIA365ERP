using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class TermRateConfiguration : IEntityTypeConfiguration<TermRate>
{
    public void Configure(EntityTypeBuilder<TermRate> builder)
    {
        builder.ToTable("LND_TermRates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_TermRates_PublicId");

        builder.Property(e => e.GuaranteeType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.AmountStart).HasPrecision(18, 2);
        builder.Property(e => e.AmountEnd).HasPrecision(18, 2);
        builder.Property(e => e.Rate).HasPrecision(6, 3);
        builder.Property(e => e.MaxAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
