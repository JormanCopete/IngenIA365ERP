using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
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
