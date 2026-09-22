using IngenIA365ERP.Domain.Entities.Payroll.ElectronicPayroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

// Feature 010, US6 (data-model §2.9). Las tablas nacen en N2 con la migración NominaPilaYNominaElectronica (D-12).

public class ElectronicPayrollSettingsConfiguration : IEntityTypeConfiguration<ElectronicPayrollSettings>
{
    public void Configure(EntityTypeBuilder<ElectronicPayrollSettings> builder)
    {
        builder.ToTable("PAY_ElectronicPayrollSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_ElectronicPayrollSettings_PublicId");

        builder.Property(e => e.EmployerTaxId).HasMaxLength(15).IsRequired();
        builder.Property(e => e.EmployerCheckDigit).HasMaxLength(1).IsRequired();
        builder.Property(e => e.EmployerBusinessName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.EmployerMunicipalityDaneCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.EmployerAddress).HasMaxLength(120).IsRequired();
        builder.Property(e => e.EmployerCountryCode).HasMaxLength(2).IsRequired();
        builder.Property(e => e.SoftwareId).HasMaxLength(36);
        builder.Property(e => e.TestSetId).HasMaxLength(36);
        builder.Property(e => e.CertificateSecretName).HasMaxLength(120);
        builder.Property(e => e.PinSecretName).HasMaxLength(120);
        builder.Property(e => e.CertificateThumbprint).HasMaxLength(64);
        builder.Property(e => e.EnabledBy).HasMaxLength(100);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ElectronicPayrollNumberingRangeConfiguration : IEntityTypeConfiguration<ElectronicPayrollNumberingRange>
{
    public void Configure(EntityTypeBuilder<ElectronicPayrollNumberingRange> builder)
    {
        builder.ToTable("PAY_ElectronicPayrollNumberingRanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_ElectronicPayrollNumberingRanges_PublicId");

        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => new { e.DocumentType, e.Environment, e.Prefix, e.ValidFrom }).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_PAY_ElectronicPayrollNumberingRanges_Type_Env_Prefix_From");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ElectronicPayrollDocumentConfiguration : IEntityTypeConfiguration<ElectronicPayrollDocument>
{
    public void Configure(EntityTypeBuilder<ElectronicPayrollDocument> builder)
    {
        builder.ToTable("PAY_ElectronicPayrollDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_ElectronicPayrollDocuments_PublicId");

        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Cune).HasMaxLength(96);
        builder.Property(e => e.PaymentDatesJson).HasMaxLength(400).IsRequired();
        builder.Property(e => e.SourceFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ZipKey).HasMaxLength(40);
        builder.Property(e => e.DianStatusCode).HasMaxLength(2);
        builder.Property(e => e.DianStatusDescription).HasMaxLength(300);
        builder.Property(e => e.QrUrl).HasMaxLength(200);
        builder.Property(e => e.TotalAccrued).HasPrecision(18, 2);
        builder.Property(e => e.TotalDeductions).HasPrecision(18, 2);
        builder.Property(e => e.TotalNet).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.Environment, e.Prefix, e.Consecutive }).IsUnique().HasDatabaseName("UK_PAY_ElectronicPayrollDocuments_Env_Prefix_Consecutive");
        builder.HasIndex(e => new { e.EmployeeId, e.Year, e.Month, e.DocumentType }).HasDatabaseName("IX_PAY_ElectronicPayrollDocuments_Employee_Period_Type");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_PAY_ElectronicPayrollDocuments_Status");
        builder.HasIndex(e => e.Cune).IsUnique().HasFilter("[Cune] IS NOT NULL").HasDatabaseName("UK_PAY_ElectronicPayrollDocuments_Cune");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.NumberingRange).WithMany().HasForeignKey(e => e.NumberingRangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AdjustsDocument).WithMany().HasForeignKey(e => e.AdjustsDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Transmissions).WithOne(t => t.Document).HasForeignKey(t => t.DocumentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ElectronicPayrollTransmissionConfiguration : IEntityTypeConfiguration<ElectronicPayrollTransmission>
{
    public void Configure(EntityTypeBuilder<ElectronicPayrollTransmission> builder)
    {
        builder.ToTable("PAY_ElectronicPayrollTransmissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_ElectronicPayrollTransmissions_PublicId");

        builder.Property(e => e.RequestedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DianStatusCode).HasMaxLength(2);
        builder.Property(e => e.StatusDescription).HasMaxLength(300);
        builder.Property(e => e.StatusMessage).HasMaxLength(1000);

        builder.HasIndex(e => new { e.DocumentId, e.Attempt }).IsUnique().HasDatabaseName("UK_PAY_ElectronicPayrollTransmissions_Document_Attempt");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
