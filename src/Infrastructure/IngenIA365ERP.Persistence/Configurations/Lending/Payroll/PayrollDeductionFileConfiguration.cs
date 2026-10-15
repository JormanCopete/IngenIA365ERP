using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionFiles</c> (feature 013, K2; data-model §24.6, §32.4): número único; los tres únicos del período —la versión, un
/// <c>Generated</c> y un <c>Sent</c> vivos— y el formato sellado. El adjunto y los mensajes son datos sin FK.
/// </summary>
public class PayrollDeductionFileConfiguration : IEntityTypeConfiguration<PayrollDeductionFile>
{
    public const string Tabla = "LND_PayrollDeductionFiles";

    public void Configure(EntityTypeBuilder<PayrollDeductionFile> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.SubPeriodNumber).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.LineCount).IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.NewCount).IsRequired();
        builder.Property(e => e.ChangeCount).IsRequired();
        builder.Property(e => e.CancellationCount).IsRequired();
        builder.Property(e => e.UnchangedCount).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.FileAttachmentPublicId).IsRequired();
        builder.Property(e => e.GeneratedAt).IsRequired();
        builder.Property(e => e.DeliveryMessagePublicId).IsRequired();
        builder.Property(e => e.PaymasterReference).HasMaxLength(60);
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasOne<PayrollAgreement>().WithMany().HasForeignKey(e => e.PayrollAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollAgreementVersion>().WithMany().HasForeignKey(e => e.PayrollAgreementVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.OutboundFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.BaseFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.SupersededByFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_PayrollDeductionFiles_Number");
        builder.HasIndex(e => new { e.PayrollAgreementId, e.PeriodYear, e.PeriodMonth, e.SubPeriodNumber, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollDeductionFiles_Agreement_Period_Version");
        builder.HasIndex(e => new { e.PayrollAgreementId, e.PeriodYear, e.PeriodMonth, e.SubPeriodNumber }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollDeductionFiles_Agreement_Period_Generated").HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.PayrollAgreementId, e.PeriodYear, e.PeriodMonth, e.SubPeriodNumber }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollDeductionFiles_Agreement_Period_Sent").HasFilter("[Status] = 3 AND [IsDeleted] = 0");
    }
}

/// <summary>
/// <c>LND_PayrollDeductionFileLines</c> (feature 013, K2; data-model §24.7): hecho hijo del archivo; número de línea único y una obligación a lo
/// sumo una vez por archivo. La cuota es <c>bigint</c> (C43).
/// </summary>
public class PayrollDeductionFileLineConfiguration : IEntityTypeConfiguration<PayrollDeductionFileLine>
{
    public const string Tabla = "LND_PayrollDeductionFileLines";

    public void Configure(EntityTypeBuilder<PayrollDeductionFileLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.LineNumber).IsRequired();
        builder.Property(e => e.EmployeeIdAtPaymaster).HasMaxLength(30);
        builder.Property(e => e.CurrentAmount).Monto().IsRequired();
        builder.Property(e => e.PastDueAmount).Monto().IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.NoveltyKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.PreviousAmount).Monto();
        builder.Property(e => e.LibranzaDate).IsRequired();

        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanPayrollLink>().WithMany().HasForeignKey(e => e.LoanPayrollLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RepaymentInstallment>().WithMany().HasForeignKey(e => e.RepaymentInstallmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollDeductionFileId, e.LineNumber }).IsUnique().HasDatabaseName("UK_LND_PayrollDeductionFileLines_File_Line");
        builder.HasIndex(e => new { e.PayrollDeductionFileId, e.LoanId }).IsUnique().HasDatabaseName("UK_LND_PayrollDeductionFileLines_File_Loan");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_PayrollDeductionFileLines_LoanId");
    }
}

/// <summary>
/// <c>LND_PayrollDeductionResults</c> (feature 013, K2; data-model §24.8): hecho; <c>UK (PayrollDeductionFileLineId, InboundMessageId)</c>: el
/// mismo mensaje no informa dos veces la misma línea. La consulta de pendientes de giro va por crédito.
/// </summary>
public class PayrollDeductionResultConfiguration : IEntityTypeConfiguration<PayrollDeductionResult>
{
    public const string Tabla = "LND_PayrollDeductionResults";

    public void Configure(EntityTypeBuilder<PayrollDeductionResult> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.DeductedAmount).Monto().IsRequired();
        builder.Property(e => e.NotDeductedAmount).Monto().IsRequired();
        builder.Property(e => e.PaymasterResultCode).HasMaxLength(20);
        builder.Property(e => e.NonDeductionReason).HasConversion<int?>();
        builder.Ignore(e => e.DejaPendienteDeGiro);

        builder.HasOne<PayrollDeductionFileLine>().WithMany().HasForeignKey(e => e.PayrollDeductionFileLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingInboundMessage>().WithMany().HasForeignKey(e => e.InboundMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollDeductionResult>().WithMany().HasForeignKey(e => e.SupersedesResultId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollDeductionFileLineId, e.InboundMessageId }).IsUnique()
            .HasDatabaseName("UK_LND_PayrollDeductionResults_Line_Message");
        builder.HasIndex(e => new { e.LoanId, e.TransferDueDate }).HasDatabaseName("IX_LND_PayrollDeductionResults_Loan_TransferDue");
        builder.HasIndex(e => e.PayrollDeductionFileId).HasDatabaseName("IX_LND_PayrollDeductionResults_FileId");
    }
}

/// <summary>
/// <c>LND_NonDeductionFollowUps</c> (feature 013, K2; data-model §24.10): hecho; el seguimiento de un no aplicado, con el mensaje al deudor o el
/// archivo que lo llevó. Una persona da motivo (CHECK).
/// </summary>
public class NonDeductionFollowUpConfiguration : IEntityTypeConfiguration<NonDeductionFollowUp>
{
    public const string Tabla = "LND_NonDeductionFollowUps";

    public void Configure(EntityTypeBuilder<NonDeductionFollowUp> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_NonDeductionFollowUps_Reason", "[IsAutomatic] = 1 OR [Reason] IS NOT NULL"));

        builder.Property(e => e.Action).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsAutomatic).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Ignore(e => e.Valido);

        builder.HasOne<InstallmentNonDeduction>().WithMany().HasForeignKey(e => e.InstallmentNonDeductionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DebtorMessage>().WithMany().HasForeignKey(e => e.DebtorMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.InstallmentNonDeductionId, e.OccurredAt }).HasDatabaseName("IX_LND_NonDeductionFollowUps_NonDeduction_At");
    }
}
