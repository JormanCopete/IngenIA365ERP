using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US7 (T0205; data-model §6.2 a §6.5): las pagadurías en Recaudo. FK Restrict a la plataforma y entre tablas RCD_; lo de Cartera (el archivo, sus líneas, los
// productos) se guarda por su PublicId, sin FK hacia LND_ (R-05).

/// <summary><c>RCD_PayrollDeductionFiles</c>: único por su mensaje; búsqueda por pagaduría y período. (nuevo)</summary>
public class PayrollDeductionFileConfiguration : IEntityTypeConfiguration<PayrollDeductionFile>
{
    public const string Tabla = "RCD_PayrollDeductionFiles";

    public void Configure(EntityTypeBuilder<PayrollDeductionFile> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.AgreementCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FileGroupCode).HasMaxLength(20);
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FileNumber).HasMaxLength(40).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.FormatCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.PastDueAmount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.FileMessagePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_FileMessagePublicId");
        builder.HasIndex(e => e.FilePublicId).HasDatabaseName($"IX_{Tabla}_FilePublicId");
        builder.HasIndex(e => new { e.PaymasterPersonId, e.PeriodYear, e.PeriodMonth }).HasDatabaseName($"IX_{Tabla}_Paymaster_Period");
    }
}

/// <summary><c>RCD_PayrollDeductionFileLines</c> (H): única por la línea de Cartera. (nuevo)</summary>
public class PayrollDeductionFileLineConfiguration : IEntityTypeConfiguration<PayrollDeductionFileLine>
{
    public const string Tabla = "RCD_PayrollDeductionFileLines";

    public void Configure(EntityTypeBuilder<PayrollDeductionFileLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.ProductNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PayerEmployeeId).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PaymasterConceptCode).HasMaxLength(20);
        builder.Property(e => e.NoveltyKind).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.PastDueAmount).Monto();
        builder.Property(e => e.DeductionOutcome).HasConversion<int?>();
        builder.Property(e => e.DeductedAmount).Monto();
        builder.Property(e => e.RemittedAmount).Monto();
        builder.HasIndex(e => e.DeductionFileLinePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_DeductionFileLinePublicId");
        builder.HasIndex(e => e.PayrollDeductionFileId).HasDatabaseName($"IX_{Tabla}_PayrollDeductionFileId");
    }
}

/// <summary><c>RCD_PayrollFileSendings</c> (H): una por versión del archivo. (nuevo)</summary>
public class PayrollFileSendingConfiguration : IEntityTypeConfiguration<PayrollFileSending>
{
    public const string Tabla = "RCD_PayrollFileSendings";

    public void Configure(EntityTypeBuilder<PayrollFileSending> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SentByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Channel).HasConversion<int>();
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.HasIndex(e => e.PayrollDeductionFileId).IsUnique().HasDatabaseName($"UK_{Tabla}_PayrollDeductionFileId");
    }
}

/// <summary><c>RCD_PaymasterResponses</c>: UK <c>(PayrollDeductionFileId, Version)</c>. (nuevo)</summary>
public class PaymasterResponseConfiguration : IEntityTypeConfiguration<PaymasterResponse>
{
    public const string Tabla = "RCD_PaymasterResponses";

    public void Configure(EntityTypeBuilder<PaymasterResponse> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.BankFileFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Source).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ResponseSha256).HasMaxLength(64);
        builder.Property(e => e.FileName).HasMaxLength(200);
        builder.Property(e => e.RejectionCode).HasMaxLength(100);
        builder.Property(e => e.RejectionDataJson).HasMaxLength(8000);
        builder.Property(e => e.Expected).Monto();
        builder.Property(e => e.Deducted).Monto();
        builder.HasIndex(e => new { e.PayrollDeductionFileId, e.Version }).IsUnique().HasDatabaseName($"UK_{Tabla}_File_Version");
    }
}

/// <summary><c>RCD_PaymasterResponseLines</c>. (nuevo)</summary>
public class PaymasterResponseLineConfiguration : IEntityTypeConfiguration<PaymasterResponseLine>
{
    public const string Tabla = "RCD_PaymasterResponseLines";

    public void Configure(EntityTypeBuilder<PaymasterResponseLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PaymasterResponse>().WithMany().HasForeignKey(e => e.PaymasterResponseId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.PayerEmployeeId).HasMaxLength(30);
        builder.Property(e => e.PaymasterConceptCode).HasMaxLength(20);
        builder.Property(e => e.ProductNumber).HasMaxLength(30);
        builder.Property(e => e.Expected).Monto();
        builder.Property(e => e.Deducted).Monto();
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.PaymasterResultCode).HasMaxLength(20);
        builder.Property(e => e.Reason).HasMaxLength(40);
        builder.HasIndex(e => e.PaymasterResponseId).HasDatabaseName($"IX_{Tabla}_PaymasterResponseId");
    }
}

/// <summary><c>RCD_PaymasterRemittances</c>: número único. (nuevo)</summary>
public class PaymasterRemittanceConfiguration : IEntityTypeConfiguration<PaymasterRemittance>
{
    public const string Tabla = "RCD_PaymasterRemittances";

    public void Configure(EntityTypeBuilder<PaymasterRemittance> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.SourceReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayerCreditBalance>().WithMany().HasForeignKey(e => e.SurplusCreditBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Source).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.PeriodsJson).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.AllocationOrder).HasConversion<int>();
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => e.BankStatementLinePublicId).HasDatabaseName($"IX_{Tabla}_BankStatementLinePublicId");
    }
}

/// <summary><c>RCD_PaymasterRemittanceLines</c>: UK por la clave de la línea. (nuevo)</summary>
public class PaymasterRemittanceLineConfiguration : IEntityTypeConfiguration<PaymasterRemittanceLine>
{
    public const string Tabla = "RCD_PaymasterRemittanceLines";

    public void Configure(EntityTypeBuilder<PaymasterRemittanceLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PaymasterRemittance>().WithMany().HasForeignKey(e => e.PaymasterRemittanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollDeductionFileLine>().WithMany().HasForeignKey(e => e.PayrollDeductionFileLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayerCreditBalance>().WithMany().HasForeignKey(e => e.CreditBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.RejectionCode).HasMaxLength(100);
        builder.Property(e => e.RejectionMessage).HasMaxLength(500);
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName($"UK_{Tabla}_IdempotencyKey");
        builder.HasIndex(e => e.PaymasterRemittanceId).HasDatabaseName($"IX_{Tabla}_PaymasterRemittanceId");
    }
}
