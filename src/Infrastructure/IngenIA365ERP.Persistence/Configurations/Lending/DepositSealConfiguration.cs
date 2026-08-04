using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class DepositSealConfiguration : IEntityTypeConfiguration<DepositSeal>
{
    public void Configure(EntityTypeBuilder<DepositSeal> builder)
    {
        builder.ToTable("LND_DepositSeals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_DepositSeals_PublicId");

        builder.HasIndex(e => e.AccountNumber).IsUnique().HasDatabaseName("UK_LND_DepositSeals_Account");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
