using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Payments;

// Feature 015, US10 (T0136; data-model §10.1 a §10.5): el recibo de mensajes, la bandeja única de órdenes, los lotes y los archivos de pago. FK Restrict a la
// plataforma (COR_People, COR_Banks, COR_PersonBankAccounts, COR_BankFileFormats, SEC_Users) y entre tablas TES_; ninguna a RCD_, LND_ ni ACC_ (R-05): lo que
// viene de otro módulo se guarda por su PublicId.

/// <summary><c>TES_InboundMessages</c>: recibo único por <c>MessagePublicId</c> sin filtro (FR-151). (nuevo)</summary>
public class TreasuryInboundMessageConfiguration : IEntityTypeConfiguration<TreasuryInboundMessage>
{
    public const string Tabla = "TES_InboundMessages";

    public void Configure(EntityTypeBuilder<TreasuryInboundMessage> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(e => e.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Type).HasMaxLength(80).IsRequired();
        builder.Property(e => e.OriginModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.Code).HasMaxLength(80);
        builder.Property(e => e.Detail).HasMaxLength(1000);
        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_MessagePublicId");
    }
}

/// <summary><c>TES_PaymentOrders</c>: UK <c>(Origin, OriginOrderPublicId)</c> sin filtro (US10-6), número único e índices de la bandeja. (nuevo)</summary>
public class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public const string Tabla = "TES_PaymentOrders";

    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DestinationBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonBankAccount>().WithMany().HasForeignKey(e => e.PersonBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(e => e.ReplacesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>();
        builder.Property(e => e.OriginOrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.OrderKind).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Form).HasConversion<int>();
        builder.Property(e => e.BeneficiaryName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.BeneficiaryTaxIdType).HasMaxLength(10);
        builder.Property(e => e.BeneficiaryTaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DestinationBankCode).HasMaxLength(10);
        builder.Property(e => e.DestinationAccountNumber).HasMaxLength(30);
        builder.Property(e => e.DestinationAccountType).HasMaxLength(20);
        builder.Property(e => e.GrossAmount).Monto();
        builder.Property(e => e.NetAmount).Monto();
        builder.Property(e => e.Concept).HasMaxLength(120).IsRequired();
        builder.Property(e => e.GmfExemptionReason).HasMaxLength(40);
        builder.Property(e => e.GmfEstimate).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.PendingReasonsJson).IsRequired();
        builder.Property(e => e.Flags).HasConversion<int>();
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.VoidedByName).HasMaxLength(150);
        builder.HasIndex(e => new { e.Origin, e.OriginOrderPublicId }).IsUnique().HasDatabaseName($"UK_{Tabla}_Origin_OriginOrderPublicId");
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.Status, e.PayDate }).HasDatabaseName($"IX_{Tabla}_Status_PayDate");
        builder.HasIndex(e => e.BeneficiaryPersonId).HasDatabaseName($"IX_{Tabla}_BeneficiaryPersonId");
        builder.HasIndex(e => e.ReplacesOrderId).HasDatabaseName($"IX_{Tabla}_ReplacesOrderId");
    }
}

/// <summary><c>TES_PaymentBatches</c>: número único; búsqueda por cuenta de origen y estado. (nuevo)</summary>
public class PaymentBatchConfiguration : IEntityTypeConfiguration<PaymentBatch>
{
    public const string Tabla = "TES_PaymentBatches";

    public void Configure(EntityTypeBuilder<PaymentBatch> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.BankFileFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PreparedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SentByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LiquidityFundUse>().WithMany().HasForeignKey(e => e.LiquidityFundUseId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Form).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Total).Monto();
        builder.Property(e => e.GmfEstimate).Monto();
        builder.Property(e => e.InputsHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.UploadedAtBankBy).HasMaxLength(150);
        builder.Property(e => e.AuthorizedAtBankBy).HasMaxLength(150);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.BankAccountId, e.Status }).HasDatabaseName($"IX_{Tabla}_BankAccountId_Status");
    }
}

/// <summary>
/// <c>TES_PaymentBatchItems</c>: una orden en a lo sumo un ítem vivo (único filtrado <c>(PaymentOrderId)</c> con <c>IsLive</c>, FR-116, SC-013); la marca la
/// baja la salida del ítem y el fin del lote (anulado o rechazado por el banco). (nuevo)
/// </summary>
public class PaymentBatchItemConfiguration : IEntityTypeConfiguration<PaymentBatchItem>
{
    public const string Tabla = "TES_PaymentBatchItems";

    public void Configure(EntityTypeBuilder<PaymentBatchItem> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<PaymentBatch>().WithMany().HasForeignKey(e => e.PaymentBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(e => e.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ExplicitlyReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.GmfEstimate).Monto();
        builder.Property(e => e.Flags).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ReviewNote).HasMaxLength(500);
        builder.Property(e => e.IsLive).HasDefaultValue(true);
        builder.HasIndex(e => e.PaymentOrderId).IsUnique().HasFilter("[IsLive] = 1 AND [IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_PaymentOrderId_Live");
        builder.HasIndex(e => e.PaymentBatchId).HasDatabaseName($"IX_{Tabla}_PaymentBatchId");
    }
}

/// <summary><c>TES_PaymentFiles</c> (H): una versión por lote; búsqueda por lote. (nuevo)</summary>
public class PaymentFileConfiguration : IEntityTypeConfiguration<PaymentFile>
{
    public const string Tabla = "TES_PaymentFiles";

    public void Configure(EntityTypeBuilder<PaymentFile> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<PaymentBatch>().WithMany().HasForeignKey(e => e.PaymentBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.BankFileFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DownloadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SentByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Total).Monto();
        builder.Property(e => e.UploadedAtBankBy).HasMaxLength(150);
        builder.Property(e => e.AuthorizedAtBankBy).HasMaxLength(150);
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.RejectedByBankReason).HasMaxLength(500);
        builder.HasIndex(e => new { e.PaymentBatchId, e.Version }).IsUnique().HasDatabaseName($"UK_{Tabla}_PaymentBatchId_Version");
    }
}

/// <summary><c>TES_PaymentFileLines</c> (H): una por ítem y archivo. (nuevo)</summary>
public class PaymentFileLineConfiguration : IEntityTypeConfiguration<PaymentFileLine>
{
    public const string Tabla = "TES_PaymentFileLines";

    public void Configure(EntityTypeBuilder<PaymentFileLine> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<PaymentFile>().WithMany().HasForeignKey(e => e.PaymentFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentBatchItem>().WithMany().HasForeignKey(e => e.PaymentBatchItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.BeneficiaryTaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BankCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.LineSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.RecordText).HasMaxLength(1000).IsRequired();
        builder.HasIndex(e => new { e.PaymentFileId, e.LineNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_PaymentFileId_LineNumber");
        builder.HasIndex(e => e.PaymentBatchItemId).HasDatabaseName($"IX_{Tabla}_PaymentBatchItemId");
    }
}
