using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PortfolioClassificationConfiguration : IEntityTypeConfiguration<PortfolioClassification>
{
    public void Configure(EntityTypeBuilder<PortfolioClassification> builder)
    {
        builder.ToTable("LND_PortfolioClassifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PortfolioClassifications_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(2).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.PersonName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);
        builder.Property(e => e.TotalBalance).HasPrecision(18, 2);
        builder.Property(e => e.InterestBalance).HasPrecision(18, 2);
        builder.Property(e => e.DefaultBalance).HasPrecision(18, 2);
        builder.Property(e => e.OrderBalance).HasPrecision(18, 2);
        builder.Property(e => e.ProvisionBalance).HasPrecision(18, 2);
        builder.Property(e => e.Rate).HasPrecision(10, 0);
        builder.Property(e => e.ContributionAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestProvision).HasPrecision(18, 2);
        builder.Property(e => e.DefaultOrderBalance).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
