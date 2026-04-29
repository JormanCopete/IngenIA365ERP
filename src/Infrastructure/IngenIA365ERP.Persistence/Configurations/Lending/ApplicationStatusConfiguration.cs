using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ApplicationStatusConfiguration : IEntityTypeConfiguration<ApplicationStatus>
{
    public void Configure(EntityTypeBuilder<ApplicationStatus> builder)
    {
        builder.ToTable("LND_ApplicationStatuses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ApplicationStatuses_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.UserFullName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DebtConsolidation).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PercentageFlag).HasMaxLength(2).IsRequired();
        builder.Property(e => e.DatacreditoRating).HasMaxLength(3).IsRequired();
        builder.Property(e => e.PaymasterDeductionType).HasMaxLength(2);
        builder.Property(e => e.PaymentCapacity).HasPrecision(18, 2);
        builder.Property(e => e.CifinScore).HasPrecision(10, 6);
        builder.Property(e => e.IndebtednessLevel).HasPrecision(15, 6);
        builder.Property(e => e.ContingencyLevel).HasPrecision(15, 6);
        builder.Property(e => e.DatacreditoScore).HasPrecision(10, 6);
        builder.Property(e => e.AverageSalary).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceExtra).HasPrecision(18, 6);
        builder.Property(e => e.CapitalAtRisk).HasPrecision(15, 3);
        builder.Property(e => e.PaymasterPercentage).HasPrecision(15, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
