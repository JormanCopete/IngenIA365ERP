using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PayrollDeductionEntryConfiguration : IEntityTypeConfiguration<PayrollDeductionEntry>
{
    public void Configure(EntityTypeBuilder<PayrollDeductionEntry> builder)
    {
        builder.ToTable("LND_PayrollDeductionEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PayrollDeductionEntries_PublicId");

        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ConceptCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.StartDate).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EndDate).HasMaxLength(20).IsRequired();
        builder.Property(e => e.OrderNumber).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(12, 0);
        builder.Property(e => e.TotalAmount).HasPrecision(12, 0);
        builder.Property(e => e.AccumulatedAmount).HasPrecision(12, 0);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
