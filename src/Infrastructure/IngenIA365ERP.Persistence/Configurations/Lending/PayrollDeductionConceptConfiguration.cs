using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PayrollDeductionConceptConfiguration : IEntityTypeConfiguration<PayrollDeductionConcept>
{
    public void Configure(EntityTypeBuilder<PayrollDeductionConcept> builder)
    {
        builder.ToTable("LND_PayrollDeductionConcepts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PayrollDeductionConcepts_PublicId");

        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.PayrollConceptCode).HasMaxLength(8).IsRequired();
        builder.Property(e => e.InterestConceptCode).HasMaxLength(8).IsRequired();
        builder.Property(e => e.ExtraConceptCode).HasMaxLength(8).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
