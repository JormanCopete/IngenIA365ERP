using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ApplicationCodebtorConfiguration : IEntityTypeConfiguration<ApplicationCodebtor>
{
    public void Configure(EntityTypeBuilder<ApplicationCodebtor> builder)
    {
        builder.ToTable("LND_ApplicationCodebtors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ApplicationCodebtors_PublicId");

        builder.Property(e => e.CodeudorCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PaymentCapacityPct).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.OtherIncome).HasPrecision(18, 2);
        builder.Property(e => e.RentalIncome).HasPrecision(18, 2);
        builder.Property(e => e.VariableIncome).HasPrecision(18, 2);
        builder.Property(e => e.EmployerDeductions).HasPrecision(18, 2);
        builder.Property(e => e.ThirdPartyDebts).HasPrecision(18, 2);
        builder.Property(e => e.OtherDeductions).HasPrecision(18, 2);
        builder.Property(e => e.MonthlyAvailable).HasPrecision(18, 2);
        builder.Property(e => e.PensionIncome).HasPrecision(18, 2);
        builder.Property(e => e.PensionDeduction).HasPrecision(18, 2);
        builder.Property(e => e.ParafiscalDeduction).HasPrecision(18, 2);
        builder.Property(e => e.PersonalExpenses).HasPrecision(15, 3);
        builder.Property(e => e.EmployerDeductionsCash).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
