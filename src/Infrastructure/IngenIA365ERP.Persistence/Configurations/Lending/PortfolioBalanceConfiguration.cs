using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PortfolioBalanceConfiguration : IEntityTypeConfiguration<PortfolioBalance>
{
    public void Configure(EntityTypeBuilder<PortfolioBalance> builder)
    {
        builder.ToTable("LND_PortfolioBalances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PortfolioBalances_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CifinStatus).HasMaxLength(3).IsRequired();
        builder.Property(e => e.InitialCategory).HasMaxLength(2).IsRequired();
        builder.Property(e => e.FinalCategory).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PaymentCycle).HasMaxLength(2);
        builder.Property(e => e.Periodicity).HasMaxLength(2);
        builder.Property(e => e.DeductionClass).HasMaxLength(2);
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);
        builder.Property(e => e.Balance).HasPrecision(18, 2);
        builder.Property(e => e.OpeningBalance).HasPrecision(18, 2);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(10, 6);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
