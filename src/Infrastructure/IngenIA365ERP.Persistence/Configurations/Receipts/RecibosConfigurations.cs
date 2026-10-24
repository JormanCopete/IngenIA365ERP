using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US3 (T0111; data-model §3): la operación de caja, el recibo, sus medios, aplicaciones y porciones, y la declaración de origen de fondos. Los
// hechos de volumen son bigint. FK Restrict a la plataforma y entre tablas RCD_. Ningún medio guarda el número de la tarjeta.

/// <summary><c>RCD_CashOperations</c>. (nuevo)</summary>
public class CashOperationConfiguration : IEntityTypeConfiguration<CashOperation>
{
    public const string Tabla = "RCD_CashOperations";

    public void Configure(EntityTypeBuilder<CashOperation> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PayerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DeliveredByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashMovement>().WithMany().HasForeignKey(e => e.CustodyMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceOfFundsDeclaration>().WithMany().HasForeignKey(e => e.SourceOfFundsDeclarationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ScreeningOutcome).HasConversion<int>();
        builder.Property(e => e.ItemsJson).IsRequired();
        builder.Property(e => e.MeansJson).IsRequired();
        builder.Property(e => e.ImputationJson).IsRequired();
        builder.Property(e => e.BackdateReason).HasMaxLength(500);
        builder.HasIndex(e => e.CashSessionId).HasDatabaseName($"IX_{Tabla}_CashSessionId");
        builder.HasIndex(e => e.PayerPersonId).HasDatabaseName($"IX_{Tabla}_PayerPersonId");
        builder.HasIndex(e => e.ReceiptId).HasDatabaseName($"IX_{Tabla}_ReceiptId");
    }
}

/// <summary><c>RCD_Receipts</c>: UK <c>(DocumentTypeId, Number)</c> y <c>OperationKey</c>; índices por oficina y fecha valor, pagador y sesión. (nuevo)</summary>
public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public const string Tabla = "RCD_Receipts";

    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<ReceiptDocumentType>().WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.CashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PayerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DeliveredByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceOfFundsDeclaration>().WithMany().HasForeignKey(e => e.SourceOfFundsDeclarationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContingencyBook>().WithMany().HasForeignKey(e => e.ContingencyBookId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Channel).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.TotalApplied).Monto();
        builder.Property(e => e.TotalMeans).Monto();
        builder.Property(e => e.ChangeGiven).Monto();
        builder.Property(e => e.BackdatedReason).HasMaxLength(500);
        builder.Property(e => e.InputsHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.VerificationCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => new { e.DocumentTypeId, e.Number }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_DocumentTypeId_Number");
        builder.HasIndex(e => e.OperationKey).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_OperationKey");
        builder.HasIndex(e => new { e.BranchId, e.ValueDate }).HasDatabaseName($"IX_{Tabla}_BranchId_ValueDate");
        builder.HasIndex(e => new { e.PayerPersonId, e.RegisteredAt }).HasDatabaseName($"IX_{Tabla}_PayerPersonId_RegisteredAt");
        builder.HasIndex(e => e.CashSessionId).HasDatabaseName($"IX_{Tabla}_CashSessionId");
    }
}

/// <summary><c>RCD_ReceiptMeans</c>: el bono de número único es único por medio entre vivos. (nuevo)</summary>
public class ReceiptMeansConfiguration : IEntityTypeConfiguration<ReceiptMeans>
{
    public const string Tabla = "RCD_ReceiptMeans";

    public void Configure(EntityTypeBuilder<ReceiptMeans> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.CardTerminalId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.MeansClass).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reference).HasMaxLength(80);
        builder.Property(e => e.TerminalBatch).HasMaxLength(40);
        builder.Property(e => e.ApprovalCode).HasMaxLength(20);
        builder.Property(e => e.VoucherNumber).HasMaxLength(40);
        builder.HasIndex(e => e.ReceiptId).HasDatabaseName($"IX_{Tabla}_ReceiptId");
        builder.HasIndex(e => new { e.PaymentMeansId, e.VoucherNumber }).IsUnique().HasFilter("[VoucherNumber] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_PaymentMeansId_VoucherNumber");
    }
}

/// <summary><c>RCD_ReceiptApplications</c>. (nuevo)</summary>
public class ReceiptApplicationConfiguration : IEntityTypeConfiguration<ReceiptApplication>
{
    public const string Tabla = "RCD_ReceiptApplications";

    public void Configure(EntityTypeBuilder<ReceiptApplication> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MiscConcept>().WithMany().HasForeignKey(e => e.MiscConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Target).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.ProductNumber).HasMaxLength(40);
        builder.Property(e => e.CrossDocumentReference).HasMaxLength(60);
        builder.Property(e => e.ModuleVoucherNumber).HasMaxLength(40);
        builder.HasIndex(e => e.ReceiptId).HasDatabaseName($"IX_{Tabla}_ReceiptId");
        builder.HasIndex(e => e.ProductPublicId).HasDatabaseName($"IX_{Tabla}_ProductPublicId");
    }
}

/// <summary><c>RCD_ReceiptApplicationPortions</c>: UK de la clave de idempotencia (R-09). (nuevo)</summary>
public class ReceiptApplicationPortionConfiguration : IEntityTypeConfiguration<ReceiptApplicationPortion>
{
    public const string Tabla = "RCD_ReceiptApplicationPortions";

    public void Configure(EntityTypeBuilder<ReceiptApplicationPortion> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<ReceiptApplication>().WithMany().HasForeignKey(e => e.ReceiptApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReceiptMeans>().WithMany().HasForeignKey(e => e.ReceiptMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.MeansClass).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ReversalKey).HasMaxLength(140);
        builder.Property(e => e.ModuleOperationNumber).HasMaxLength(40);
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_IdempotencyKey");
        builder.HasIndex(e => e.ReceiptApplicationId).HasDatabaseName($"IX_{Tabla}_ReceiptApplicationId");
        builder.HasIndex(e => e.ReceiptMeansId).HasDatabaseName($"IX_{Tabla}_ReceiptMeansId");
    }
}

/// <summary><c>RCD_SourceOfFundsDeclarations</c>. (nuevo)</summary>
public class SourceOfFundsDeclarationConfiguration : IEntityTypeConfiguration<SourceOfFundsDeclaration>
{
    public const string Tabla = "RCD_SourceOfFundsDeclarations";

    public void Configure(EntityTypeBuilder<SourceOfFundsDeclaration> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.CashAmount).Monto();
        builder.Property(e => e.Origin).HasMaxLength(500).IsRequired();
        builder.Property(e => e.RequiredBy).HasConversion<int>();
        builder.HasIndex(e => e.ReceiptOperationPublicId).HasDatabaseName($"IX_{Tabla}_ReceiptOperationPublicId");
    }
}
