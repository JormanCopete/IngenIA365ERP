using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollAgreements</c> (feature 013, K2; data-model §24.1): código único entre vivos (<c>CodigoDeCatalogo</c>, 10) y un solo
/// convenio de nómina propia vivo (<c>UK (IsOwnPayroll)</c> filtrado). La pagaduría es una persona del maestro. Migración <c>CarteraLibranza</c>.
/// </summary>
public class PayrollAgreementConfiguration : IEntityTypeConfiguration<PayrollAgreement>
{
    public const string Tabla = "LND_PayrollAgreements";

    public void Configure(EntityTypeBuilder<PayrollAgreement> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsOwnPayroll).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Description).HasMaxLength(300);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_PayrollAgreements_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.IsOwnPayroll).IsUnique()
            .HasDatabaseName("UK_LND_PayrollAgreements_OwnPayroll").HasFilter("[IsOwnPayroll] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.PaymasterPersonId).HasDatabaseName("IX_LND_PayrollAgreements_PaymasterPersonId");
    }
}

/// <summary>
/// <c>LND_PayrollAgreementVersions</c> (feature 013, K2; data-model §24.2): <c>UK (PayrollAgreementId, ValidFrom)</c>; formatos de ida y vuelta
/// por referencia a <c>COR_BankFileFormats</c>; tope propio como fracción (9,6). El no cruce de vigencias lo hace cumplir el comando.
/// </summary>
public class PayrollAgreementVersionConfiguration : IEntityTypeConfiguration<PayrollAgreementVersion>
{
    public const string Tabla = "LND_PayrollAgreementVersions";

    public void Configure(EntityTypeBuilder<PayrollAgreementVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.CutoffDay).IsRequired();
        builder.Property(e => e.PaymentDay).IsRequired();
        builder.Property(e => e.SendLeadDays).IsRequired();
        builder.Property(e => e.DeductionCapFraction).Fraccion();
        builder.Property(e => e.IncludesPastDue).IsRequired();
        builder.Property(e => e.NoveltyRulesJson).IsRequired();
        builder.Property(e => e.NonDeductionAction).HasConversion<int>().IsRequired();
        builder.Property(e => e.AlternateRepaymentChannel).HasConversion<int>().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        // Feature 014 (T028; data-model §1.4): grupos de archivo, identificación del empleado y cuotas de cuentas; sin grupos, los dos formatos de la
        // versión; con grupos, ninguno (cada grupo lleva los suyos).
        builder.Property(e => e.UsesFileGroups).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.EmployeeIdentification).HasMaxLength(20).IsRequired()
            .HasDefaultValue(PayrollAgreementVersion.IdentificacionPorCodigoDeEmpleado);
        builder.Property(e => e.IncludesAccountInstallments).IsRequired().HasDefaultValue(false);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_PayrollAgreementVersions_Formats",
            "([UsesFileGroups] = 0 AND [OutboundFormatId] IS NOT NULL AND [ResultFormatId] IS NOT NULL) OR "
            + "([UsesFileGroups] = 1 AND [OutboundFormatId] IS NULL AND [ResultFormatId] IS NULL)"));

        builder.HasOne<PayrollAgreement>().WithMany().HasForeignKey(e => e.PayrollAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.OutboundFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.ResultFormatId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollAgreementId, e.ValidFrom }).IsUnique().HasDatabaseName("UK_LND_PayrollAgreementVersions_Agreement_From");
    }
}

/// <summary>
/// <c>LND_PayrollAgreementContacts</c> (feature 013, K2; data-model §24.3): <c>UK (PayrollAgreementId, PersonId, Role, ValidFrom)</c>; el
/// contacto es una persona del maestro.
/// </summary>
public class PayrollAgreementContactConfiguration : IEntityTypeConfiguration<PayrollAgreementContact>
{
    public const string Tabla = "LND_PayrollAgreementContacts";

    public void Configure(EntityTypeBuilder<PayrollAgreementContact> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Role).HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsPrimary).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();

        builder.HasOne<PayrollAgreement>().WithMany().HasForeignKey(e => e.PayrollAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollAgreementId, e.PersonId, e.Role, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollAgreementContacts_Agr_Person_Role_From");
    }
}

/// <summary>
/// <c>LND_PayrollAgreementResultCodes</c> (feature 013, K2; data-model §24.4): <c>UK (PayrollAgreementVersionId, PaymasterCode)</c>; el motivo
/// obligatorio con <c>NotDeducted</c> y <c>PartiallyDeducted</c> va como CHECK.
/// </summary>
public class PayrollAgreementResultCodeConfiguration : IEntityTypeConfiguration<PayrollAgreementResultCode>
{
    public const string Tabla = "LND_PayrollAgreementResultCodes";

    public void Configure(EntityTypeBuilder<PayrollAgreementResultCode> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_PayrollAgreementResultCodes_Reason",
            "([Outcome] = 1 AND [NonDeductionReason] IS NULL) OR ([Outcome] <> 1 AND [NonDeductionReason] IS NOT NULL)"));

        builder.Property(e => e.PaymasterCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.NonDeductionReason).HasConversion<int?>();
        builder.Ignore(e => e.Valido);

        builder.HasOne<PayrollAgreementVersion>().WithMany().HasForeignKey(e => e.PayrollAgreementVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollAgreementVersionId, e.PaymasterCode }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollAgreementResultCodes_Version_Code");
    }
}

/// <summary>
/// <c>LND_PayrollAgreementFileGroups</c> (feature 014, K7, T090; data-model §1.4.1): <c>UK (PayrollAgreementVersionId, Code)</c>; los formatos de ida y
/// vuelta por referencia a <c>COR_BankFileFormats</c>; calendario propio opcional. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class PayrollAgreementFileGroupConfiguration : IEntityTypeConfiguration<PayrollAgreementFileGroup>
{
    public const string Tabla = "LND_PayrollAgreementFileGroups";

    public void Configure(EntityTypeBuilder<PayrollAgreementFileGroup> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Order).IsRequired();

        builder.HasOne<PayrollAgreementVersion>().WithMany().HasForeignKey(e => e.PayrollAgreementVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.OutboundFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.ResultFormatId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollAgreementVersionId, e.Code }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollAgreementFileGroups_Version_Code");
    }
}

/// <summary>
/// <c>LND_PayrollAgreementConceptEquivalences</c> (feature 014, K7, T090; data-model §1.4.2): <c>UK (PayrollAgreementVersionId, PaymasterConceptCode)</c>;
/// el grupo de archivo opcional (obligatorio si la versión usa grupos, lo dice el comando); los ítems en JSON canónico.
/// </summary>
public class PayrollAgreementConceptEquivalenceConfiguration : IEntityTypeConfiguration<PayrollAgreementConceptEquivalence>
{
    public const string Tabla = "LND_PayrollAgreementConceptEquivalences";

    public void Configure(EntityTypeBuilder<PayrollAgreementConceptEquivalence> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.PaymasterConceptCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.AllocationRule).HasConversion<int>().IsRequired();
        builder.Property(e => e.Priority).IsRequired();
        builder.Property(e => e.ItemsJson).IsRequired();

        builder.HasOne<PayrollAgreementVersion>().WithMany().HasForeignKey(e => e.PayrollAgreementVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollAgreementFileGroup>().WithMany().HasForeignKey(e => e.FileGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollAgreementVersionId, e.PaymasterConceptCode }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollAgreementConceptEquivalences_Version_Code");
        builder.HasIndex(e => e.FileGroupId).HasDatabaseName("IX_LND_PayrollAgreementConceptEquivalences_FileGroupId");
    }
}
