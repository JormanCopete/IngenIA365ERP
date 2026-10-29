using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US5 (T0174; data-model §4.1 a §4.3, §4.5, §6.1) y la activación mínima (T0183b; §15.1): la anulación, los reversos de los módulos, los saldos a
// favor, la cuenta por cobrar al pagador, el recibo de mensajes y las activaciones. FK Restrict a la plataforma y entre tablas RCD_; nada hacia LND_ ni TES_ (lo que
// viene de otro módulo se guarda por su PublicId).

/// <summary><c>RCD_ReceiptVoidings</c> (H): una por recibo. (nuevo)</summary>
public class ReceiptVoidingConfiguration : IEntityTypeConfiguration<ReceiptVoiding>
{
    public const string Tabla = "RCD_ReceiptVoidings";

    public void Configure(EntityTypeBuilder<ReceiptVoiding> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayerCreditBalance>().WithMany().HasForeignKey(e => e.PayerCreditBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30);
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.DecisionReason).HasMaxLength(500);
        builder.Property(e => e.OmittedApplicationsJson).HasMaxLength(4000);
        builder.Property(e => e.VerificationCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.ReceiptId).IsUnique().HasDatabaseName($"UK_{Tabla}_ReceiptId");
        builder.HasIndex(e => e.Number).HasDatabaseName($"IX_{Tabla}_Number");
    }
}

/// <summary><c>RCD_ModuleReversals</c> (H): una por porción reversada por el módulo. (nuevo)</summary>
public class ModuleReversalConfiguration : IEntityTypeConfiguration<ModuleReversal>
{
    public const string Tabla = "RCD_ModuleReversals";

    public void Configure(EntityTypeBuilder<ModuleReversal> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<ReceiptsInboundMessage>().WithMany().HasForeignKey(e => e.InboundMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReceiptApplication>().WithMany().HasForeignKey(e => e.ReceiptApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReceiptApplicationPortion>().WithMany().HasForeignKey(e => e.PortionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayerCreditBalance>().WithMany().HasForeignKey(e => e.PayerCreditBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Module).HasMaxLength(10).IsRequired();
        builder.Property(e => e.MessageType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ModuleReversalNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Cause).HasConversion<int>();
        builder.Property(e => e.ConfirmationNote).HasMaxLength(500);
        builder.HasIndex(e => e.PortionId).IsUnique().HasFilter("[PortionId] IS NOT NULL AND [IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_PortionId");
        builder.HasIndex(e => e.Cause).HasDatabaseName($"IX_{Tabla}_Cause");
    }
}

/// <summary><c>RCD_PayerCreditBalances</c>. (nuevo)</summary>
public class PayerCreditBalanceConfiguration : IEntityTypeConfiguration<PayerCreditBalance>
{
    public const string Tabla = "RCD_PayerCreditBalances";

    public void Configure(EntityTypeBuilder<PayerCreditBalance> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Origin).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.OriginDocumentNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Balance).Monto();
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName($"IX_{Tabla}_PersonId_Status");
        builder.HasIndex(e => new { e.Origin, e.OriginDocumentPublicId }).HasDatabaseName($"IX_{Tabla}_Origin_OriginDocumentPublicId");
    }
}

/// <summary><c>RCD_PayerCreditBalanceMovements</c> (H). (nuevo)</summary>
public class PayerCreditBalanceMovementConfiguration : IEntityTypeConfiguration<PayerCreditBalanceMovement>
{
    public const string Tabla = "RCD_PayerCreditBalanceMovements";

    public void Configure(EntityTypeBuilder<PayerCreditBalanceMovement> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PayerCreditBalance>().WithMany().HasForeignKey(e => e.PayerCreditBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.DocumentNumber).HasMaxLength(30);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.PayerCreditBalanceId).HasDatabaseName($"IX_{Tabla}_PayerCreditBalanceId");
        builder.HasIndex(e => e.DocumentPublicId).HasDatabaseName($"IX_{Tabla}_DocumentPublicId");
    }
}

/// <summary><c>RCD_PayerReceivables</c>. (nuevo)</summary>
public class PayerReceivableConfiguration : IEntityTypeConfiguration<PayerReceivable>
{
    public const string Tabla = "RCD_PayerReceivables";

    public void Configure(EntityTypeBuilder<PayerReceivable> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.OriginDocumentNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Balance).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName($"IX_{Tabla}_PersonId_Status");
    }
}

/// <summary><c>RCD_InboundMessages</c>: recibo único por <c>MessagePublicId</c> sin filtro (FR-151). (nuevo)</summary>
public class ReceiptsInboundMessageConfiguration : IEntityTypeConfiguration<ReceiptsInboundMessage>
{
    public const string Tabla = "RCD_InboundMessages";

    public void Configure(EntityTypeBuilder<ReceiptsInboundMessage> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Type).HasMaxLength(80).IsRequired();
        builder.Property(e => e.OriginModule).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.Code).HasMaxLength(100);
        builder.Property(e => e.Detail).HasMaxLength(1000);
        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_MessagePublicId");
    }
}

/// <summary><c>RCD_Activations</c> (T0183b; data-model §15.1). La FK de <c>OpeningSquareId</c> llega con <c>RecaudoYTesoreriaPuestaEnMarcha</c>. (nuevo)</summary>
public class ReceiptsActivationConfiguration : IEntityTypeConfiguration<ReceiptsActivation>
{
    public const string Tabla = "RCD_Activations";

    public void Configure(EntityTypeBuilder<ReceiptsActivation> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.RequirementsJson).HasMaxLength(8000).IsRequired();
        builder.HasIndex(e => e.ActivatedOn).HasDatabaseName($"IX_{Tabla}_ActivatedOn");
        // US13 (T0291): la FK del cuadre llega con el par RecaudoYTesoreriaPuestaEnMarcha.
        builder.HasOne<ReceiptsOpeningSquare>().WithMany().HasForeignKey(e => e.OpeningSquareId).OnDelete(DeleteBehavior.Restrict);
    }
}
