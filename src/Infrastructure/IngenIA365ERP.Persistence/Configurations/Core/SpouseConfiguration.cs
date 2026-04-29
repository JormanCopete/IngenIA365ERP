using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class SpouseConfiguration : IEntityTypeConfiguration<Spouse>
{
    public void Configure(EntityTypeBuilder<Spouse> builder)
    {
        builder.ToTable("COR_Spouses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Spouses_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.SpouseName).HasMaxLength(80);
        builder.Property(e => e.SpouseIdNumber).HasMaxLength(20);
        builder.Property(e => e.SpouseIdType).HasMaxLength(2);
        builder.Property(e => e.SpouseIdIssuedAt).HasMaxLength(80);
        builder.Property(e => e.SpouseAddress).HasMaxLength(80);
        builder.Property(e => e.SpouseEmployer).HasMaxLength(80);
        builder.Property(e => e.SpouseEmployerAddress).HasMaxLength(80);
        builder.Property(e => e.SpousePhone).HasMaxLength(30);
        builder.Property(e => e.SpouseCity).HasMaxLength(40);
        builder.Property(e => e.SpouseProfession).HasMaxLength(10);
        builder.Property(e => e.SpousePosition).HasMaxLength(60);
        builder.Property(e => e.SpouseSalary).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.SpouseGender).HasMaxLength(2);
        builder.Property(e => e.SpouseFax).HasMaxLength(30);
        builder.Property(e => e.SpouseMailingPref).HasMaxLength(2);
        builder.Property(e => e.SpouseMailingAddress).HasMaxLength(120);
        builder.Property(e => e.SpouseCompanyCode).HasMaxLength(10);
        builder.Property(e => e.SpouseBranchCode).HasMaxLength(10);
        builder.Property(e => e.SpouseSectionCode).HasMaxLength(10);
        builder.Property(e => e.SpouseEducationLevel).HasMaxLength(2);
        builder.Property(e => e.SpouseSalaryType).HasMaxLength(2);
        builder.Property(e => e.SpouseSeverance).HasPrecision(17, 2);
        builder.Property(e => e.SpouseOtherIncome).HasPrecision(17, 2);
        builder.Property(e => e.SpouseOtherIncomeDesc).HasMaxLength(120);

        // Unique: one spouse per person
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_COR_Spouses_PersonId");

        // FK index
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_COR_Spouses_PersonId");

        // Relationship configured from PersonConfiguration (Cascade)

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
