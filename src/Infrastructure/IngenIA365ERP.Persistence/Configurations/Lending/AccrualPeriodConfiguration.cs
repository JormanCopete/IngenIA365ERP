using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class AccrualPeriodConfiguration : IEntityTypeConfiguration<AccrualPeriod>
{
    public void Configure(EntityTypeBuilder<AccrualPeriod> builder)
    {
        builder.ToTable("LND_AccrualPeriods");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_AccrualPeriods_PublicId");

        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PaymentCycle).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AccruedAmount).HasPrecision(18, 2);
        builder.Property(e => e.UsuryRate).HasPrecision(6, 3);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
