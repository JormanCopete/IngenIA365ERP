using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class ConceptAccountConfiguration : IEntityTypeConfiguration<ConceptAccount>
{
    public void Configure(EntityTypeBuilder<ConceptAccount> builder)
    {
        builder.ToTable("PAY_ConceptAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.ConceptId, e.CostCenterId }).IsUnique();

        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.ExpenseAccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.CounterAccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.ProvisionAccountCode).HasMaxLength(15).IsRequired();

        builder.HasOne(e => e.Concept).WithMany().HasForeignKey(e => e.ConceptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
