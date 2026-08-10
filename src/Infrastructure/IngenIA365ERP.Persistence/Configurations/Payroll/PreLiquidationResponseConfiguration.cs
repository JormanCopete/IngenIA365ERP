using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PreLiquidationResponseConfiguration : IEntityTypeConfiguration<PreLiquidationResponse>
{
    public void Configure(EntityTypeBuilder<PreLiquidationResponse> builder)
    {
        builder.ToTable("PAY_PreLiquidationResponses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.PilaCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.WorkRiskAuth).HasMaxLength(15).IsRequired();
        builder.Property(e => e.EntityClass).HasMaxLength(5).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
