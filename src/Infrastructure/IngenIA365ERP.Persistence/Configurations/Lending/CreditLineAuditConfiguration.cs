using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CreditLineAuditConfiguration : IEntityTypeConfiguration<CreditLineAudit>
{
    public void Configure(EntityTypeBuilder<CreditLineAudit> builder)
    {
        builder.ToTable("LND_CreditLineAudit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CreditLineAudit_PublicId");

        builder.Property(e => e.Action).HasMaxLength(2).IsRequired();
        builder.Property(e => e.InterestRate_Old).HasPrecision(10, 5);
        builder.Property(e => e.InterestRate_New).HasPrecision(10, 5);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
