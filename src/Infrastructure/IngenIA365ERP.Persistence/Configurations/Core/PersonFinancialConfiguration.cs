using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class PersonFinancialConfiguration : IEntityTypeConfiguration<PersonFinancial>
{
    public void Configure(EntityTypeBuilder<PersonFinancial> builder)
    {
        builder.ToTable("COR_PeopleFinancial");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_PeopleFinancial_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.DebtCapacity).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.OtherIncome).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalAssets).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.VariableIncome).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.RentalIncome).HasPrecision(17, 2);
        builder.Property(e => e.PensionIncome).HasPrecision(17, 2);
        builder.Property(e => e.ThirdPartyDebts).HasPrecision(17, 2);
        builder.Property(e => e.MonthlyFixedExpenses).HasPrecision(17, 2);
        builder.Property(e => e.PersonalExpenses).HasPrecision(17, 2);
        builder.Property(e => e.PensionDeduction).HasPrecision(17, 2);
        builder.Property(e => e.CreditScore).HasPrecision(6, 2).HasDefaultValue(0m);
        builder.Property(e => e.CreditBureauScore).HasPrecision(15, 3).HasDefaultValue(0m);
        builder.Property(e => e.CreditBureauRating).HasMaxLength(4);
        builder.Property(e => e.ExternalDebtPayment).HasPrecision(15, 3).HasDefaultValue(0m);
        builder.Property(e => e.ExternalDebtBalance).HasPrecision(15, 3).HasDefaultValue(0m);
        builder.Property(e => e.PastDueCreditBureau).HasPrecision(15, 3).HasDefaultValue(0m);

        // Unique: one financial record per person
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_COR_PeopleFinancial_PersonId");

        // FK index
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_COR_PeopleFinancial_PersonId");

        // Relationship configured from PersonConfiguration (Cascade)

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
