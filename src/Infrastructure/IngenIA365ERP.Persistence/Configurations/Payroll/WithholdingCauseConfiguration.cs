using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class WithholdingCauseConfiguration : IEntityTypeConfiguration<WithholdingCause>
{
    public void Configure(EntityTypeBuilder<WithholdingCause> builder)
    {
        builder.ToTable("PAY_WithholdingCauses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.Code).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
