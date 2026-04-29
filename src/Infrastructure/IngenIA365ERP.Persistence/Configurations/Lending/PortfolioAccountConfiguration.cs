using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PortfolioAccountConfiguration : IEntityTypeConfiguration<PortfolioAccount>
{
    public void Configure(EntityTypeBuilder<PortfolioAccount> builder)
    {
        builder.ToTable("LND_PortfolioAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PortfolioAccounts_PublicId");

        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.RecordClass).HasMaxLength(3).IsRequired();
        builder.Property(e => e.RiskLevel).HasMaxLength(2).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
