using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CreditParameterConfiguration : IEntityTypeConfiguration<CreditParameter>
{
    public void Configure(EntityTypeBuilder<CreditParameter> builder)
    {
        builder.ToTable("LND_CreditParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CreditParameters_PublicId");

        builder.Property(e => e.BankCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.DeductionClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CreditLimit).HasPrecision(10, 0);
        builder.Property(e => e.TaxRate).HasPrecision(10, 5);
        builder.Property(e => e.MaintenanceAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
