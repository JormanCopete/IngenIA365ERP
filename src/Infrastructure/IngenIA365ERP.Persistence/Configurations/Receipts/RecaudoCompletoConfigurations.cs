using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, R1 (par RecaudoCompleto; data-model §7.3, §8.1; DT §2.3): las tablas RCD_ de R1. FK Restrict a la plataforma y entre tablas RCD_; nada hacia LND_ ni
// TES_ (lo que viene de otro módulo se guarda por su PublicId). Cada fase de R1 suma las suyas y regenera el par.

/// <summary><c>RCD_PendingApplications</c> (H): el recaudo pendiente de aplicar, uno por aplicación del recibo (FR-046). (nuevo)</summary>
public class PendingApplicationConfiguration : IEntityTypeConfiguration<PendingApplication>
{
    public const string Tabla = "RCD_PendingApplications";

    public void Configure(EntityTypeBuilder<PendingApplication> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReceiptApplication>().WithMany().HasForeignKey(e => e.ReceiptApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.TargetModule).HasConversion<int>();
        builder.Property(e => e.ProductReference).HasMaxLength(40);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.ReceiptApplicationId).IsUnique().HasDatabaseName($"UK_{Tabla}_ReceiptApplicationId");
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName($"IX_{Tabla}_PersonId_Status");
    }
}

/// <summary><c>RCD_PendingApplicationResolutions</c> (H): la aplicación del pendiente por el contrato del módulo ya activado; una por pendiente. (nuevo)</summary>
public class PendingApplicationResolutionConfiguration : IEntityTypeConfiguration<PendingApplicationResolution>
{
    public const string Tabla = "RCD_PendingApplicationResolutions";

    public void Configure(EntityTypeBuilder<PendingApplicationResolution> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PendingApplication>().WithMany().HasForeignKey(e => e.PendingApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.ProductNumber).HasMaxLength(40);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ModuleOperationNumber).HasMaxLength(40);
        builder.HasIndex(e => e.PendingApplicationId).IsUnique().HasDatabaseName($"UK_{Tabla}_PendingApplicationId");
    }
}

/// <summary><c>RCD_CashDisbursements</c> (H): el egreso de caja (FR-051 a FR-056; data-model §8.1). Numerado por tipo y con la clave de la operación únicos. (nuevo)</summary>
public class CashDisbursementConfiguration : IEntityTypeConfiguration<CashDisbursement>
{
    public const string Tabla = "RCD_CashDisbursements";

    public void Configure(EntityTypeBuilder<CashDisbursement> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<ReceiptDocumentType>().WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.CashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.ReceivedByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayerCreditBalance>().WithMany().HasForeignKey(e => e.PayerCreditBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MiscConcept>().WithMany().HasForeignKey(e => e.MiscConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.ProductNumber).HasMaxLength(40);
        builder.Property(e => e.ProductNature).HasMaxLength(20);
        builder.Property(e => e.ModuleAuthorizationNumber).HasMaxLength(40);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.GmfAmount).Monto();
        builder.Property(e => e.SignersJson).HasMaxLength(4000);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.VerificationCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.DocumentTypeId, e.Number }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_DocumentTypeId_Number");
        builder.HasIndex(e => e.OperationKey).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_OperationKey");
        builder.HasIndex(e => new { e.CashSessionId, e.Status }).HasDatabaseName($"IX_{Tabla}_CashSessionId_Status");
        builder.HasIndex(e => new { e.CashBoxId, e.OperationDate }).HasDatabaseName($"IX_{Tabla}_CashBoxId_OperationDate");
        builder.HasIndex(e => new { e.BeneficiaryPersonId, e.OperationDate }).HasDatabaseName($"IX_{Tabla}_BeneficiaryPersonId_OperationDate");
    }
}

/// <summary><c>RCD_CashDisbursementVoidings</c> (H): la anulación de un egreso, una por egreso (FR-061). (nuevo)</summary>
public class CashDisbursementVoidingConfiguration : IEntityTypeConfiguration<CashDisbursementVoiding>
{
    public const string Tabla = "RCD_CashDisbursementVoidings";

    public void Configure(EntityTypeBuilder<CashDisbursementVoiding> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashDisbursement>().WithMany().HasForeignKey(e => e.CashDisbursementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ModuleReversalNumber).HasMaxLength(40);
        builder.Property(e => e.VerificationCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.CashDisbursementId).IsUnique().HasDatabaseName($"UK_{Tabla}_CashDisbursementId");
    }
}

/// <summary><c>RCD_IdentificationRules</c>: las reglas de identificación de abonos (FR-067; D15). Código único entre vivas. (nuevo)</summary>
public class IdentificationRuleConfiguration : IEntityTypeConfiguration<IdentificationRule>
{
    public const string Tabla = "RCD_IdentificationRules";

    public void Configure(EntityTypeBuilder<IdentificationRule> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Criterion).HasConversion<int>();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Code");
        builder.HasIndex(e => new { e.IsActive, e.Priority }).HasDatabaseName($"IX_{Tabla}_IsActive_Priority");
    }
}

/// <summary><c>RCD_ReportedDeposits</c>: la consignación reportada en ventanilla (FR-069; D11). La línea del extracto, por su PublicId. (nuevo)</summary>
public class ReportedDepositConfiguration : IEntityTypeConfiguration<ReportedDeposit>
{
    public const string Tabla = "RCD_ReportedDeposits";

    public void Configure(EntityTypeBuilder<ReportedDeposit> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Target).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.HasIndex(e => e.Number).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.OwnBankAccountPublicId, e.Status, e.DepositDate }).HasDatabaseName($"IX_{Tabla}_Account_Status_Date");
        builder.HasIndex(e => e.StatementLinePublicId).HasDatabaseName($"IX_{Tabla}_StatementLinePublicId");
    }
}

/// <summary><c>RCD_PaymasterCalendar</c>: la fecha de pago de la nómina de una pagaduría y su fecha legal de giro (FR-082), una por pagaduría y período. (nuevo)</summary>
public class PaymasterCalendarEntryConfiguration : IEntityTypeConfiguration<PaymasterCalendarEntry>
{
    public const string Tabla = "RCD_PaymasterCalendar";

    public void Configure(EntityTypeBuilder<PaymasterCalendarEntry> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.PaymasterPersonId, e.PeriodYear, e.PeriodMonth, e.SubPeriod }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_Paymaster_Period");
        builder.HasIndex(e => e.LegalRemittanceDate).HasDatabaseName($"IX_{Tabla}_LegalRemittanceDate");
    }
}

/// <summary><c>RCD_CashTransactionReports</c>: una generación del reporte de transacciones de un mes (FR-096; data-model §7.3). Versión única por período. (nuevo)</summary>
public class CashTransactionReportConfiguration : IEntityTypeConfiguration<CashTransactionReport>
{
    public const string Tabla = "RCD_CashTransactionReports";

    public void Configure(EntityTypeBuilder<CashTransactionReport> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Scope).HasConversion<int>();
        builder.Property(e => e.ApplicationState).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.LayoutCode).HasMaxLength(80).IsRequired();
        builder.Property(e => e.LayoutVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(200);
        builder.Property(e => e.Sha256).HasMaxLength(64);
        builder.Property(e => e.UploadReference).HasMaxLength(60);
        builder.Property(e => e.IssuesJson).IsRequired();
        builder.Property(e => e.Total).Monto();
        builder.HasIndex(e => new { e.PeriodYear, e.PeriodMonth, e.Version }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Period_Version");
    }
}

/// <summary><c>RCD_CashTransactionReportLines</c> (H): las líneas del reporte (individuales y múltiples). (nuevo)</summary>
public class CashTransactionReportLineConfiguration : IEntityTypeConfiguration<CashTransactionReportLine>
{
    public const string Tabla = "RCD_CashTransactionReportLines";

    public void Configure(EntityTypeBuilder<CashTransactionReportLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashTransactionReport>().WithMany().HasForeignKey(e => e.CashTransactionReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.DocumentNumber).HasMaxLength(30);
        builder.Property(e => e.DocumentsJson).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(30).IsRequired();
        builder.HasIndex(e => new { e.CashTransactionReportId, e.LineNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_Report_Line");
    }
}
