using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PayrollConceptConfiguration : IEntityTypeConfiguration<PayrollConcept>
{
    public void Configure(EntityTypeBuilder<PayrollConcept> builder)
    {
        builder.ToTable("PAY_PayrollConcepts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.ConceptCode).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DaysComputed).HasMaxLength(2).IsRequired();
        builder.Property(e => e.CertificateLine).HasMaxLength(4).IsRequired();
        builder.Property(e => e.CertificateColumn).HasMaxLength(4).IsRequired();
        builder.Property(e => e.Priority).HasMaxLength(2).IsRequired();
        builder.Property(e => e.TaxId).HasMaxLength(14).IsRequired();
        builder.Property(e => e.EquivalentCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.AdminId).HasMaxLength(6).IsRequired();
        builder.Property(e => e.IsAutomatic).HasMaxLength(1).IsRequired();

        builder.Property(e => e.Value).HasPrecision(18, 2);
        builder.Property(e => e.Factor).HasPrecision(12, 2);
        builder.Property(e => e.TopSalary).HasPrecision(18, 2);
        builder.Property(e => e.ProvisionRate).HasPrecision(6, 3);
        builder.Property(e => e.VatRate).HasPrecision(6, 3);
        builder.Property(e => e.MinorRate).HasPrecision(4, 2);
        builder.Property(e => e.MajorRate).HasPrecision(4, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
