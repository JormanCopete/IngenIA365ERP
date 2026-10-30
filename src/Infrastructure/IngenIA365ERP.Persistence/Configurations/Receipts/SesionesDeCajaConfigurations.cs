using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US2 (T0090; data-model §2, §4.4): las tablas de la sesión de caja, el arqueo, la bóveda, el cierre del día, el cuadre diario, los faltantes y
// los sobrantes. FK Restrict a la plataforma y entre tablas RCD_. Valores por alias de PrecisionDeRecaudo; cantidades de billetes como int. Enums como int.

/// <summary>
/// <c>RCD_CollectionPointDays</c>: UK <c>(CollectionPointId, BusinessDate)</c>. (nuevo)
/// </summary>
public class CollectionPointDayConfiguration : IEntityTypeConfiguration<CollectionPointDay>
{
    public const string Tabla = "RCD_CollectionPointDays";

    public void Configure(EntityTypeBuilder<CollectionPointDay> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.CollectionPointId, e.BusinessDate }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_Point_Date");
    }
}

/// <summary>
/// <c>RCD_CashSessions</c>: una sesión viva por caja (único filtrado <c>(CashBoxId)</c> en <c>Open</c>, <c>Closing</c>, <c>ForgottenOpen</c>). La de una por
/// cajero depende de <c>Caja.UnaSesionPorCajero</c>, que es un parámetro con vigencia: la base no puede exigirla, así que el índice de <c>(CashierUserId)</c>
/// en esos estados es de búsqueda y la exclusividad la comprueba la apertura bajo el cerrojo del cajero (DT §2.7). (nuevo)
/// </summary>
public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public const string Tabla = "RCD_CashSessions";
    private const string Vivas = "[Status] IN (1, 2, 5) AND [IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.CashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionPointDay>().WithMany().HasForeignKey(e => e.CollectionPointDayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.WitnessUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.OpeningBase).Monto();
        builder.Property(e => e.SealedSettingsJson).IsRequired();
        builder.Property(e => e.ExpectedByMeansJson).IsRequired();
        builder.Ignore(e => e.Recibe);
        builder.HasIndex(e => e.CashBoxId).IsUnique().HasFilter(Vivas).HasDatabaseName($"UK_{Tabla}_CashBoxId_Live");
        builder.HasIndex(e => e.CashierUserId).HasFilter(Vivas).HasDatabaseName($"IX_{Tabla}_CashierUserId_Live");
        builder.HasIndex(e => e.CollectionPointDayId).HasDatabaseName($"IX_{Tabla}_CollectionPointDayId");
    }
}

/// <summary><c>RCD_CashCounts</c>. (nuevo)</summary>
public class CashCountConfiguration : IEntityTypeConfiguration<CashCount>
{
    public const string Tabla = "RCD_CashCounts";

    public void Configure(EntityTypeBuilder<CashCount> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vault>().WithMany().HasForeignKey(e => e.VaultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CountedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.WitnessUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.VerificationCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.CashSessionId).HasDatabaseName($"IX_{Tabla}_CashSessionId");
        builder.HasIndex(e => e.VaultId).HasDatabaseName($"IX_{Tabla}_VaultId");
    }
}

/// <summary><c>RCD_CashCountLines</c>: una línea por medio en cada arqueo. (nuevo)</summary>
public class CashCountLineConfiguration : IEntityTypeConfiguration<CashCountLine>
{
    public const string Tabla = "RCD_CashCountLines";

    public void Configure(EntityTypeBuilder<CashCountLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashCount>().WithMany().HasForeignKey(e => e.CashCountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Method).HasConversion<int>();
        builder.Property(e => e.Treatment).HasConversion<int>();
        builder.Property(e => e.Expected).Monto();
        builder.Property(e => e.Counted).Monto();
        builder.Property(e => e.Difference).Monto();
        builder.Property(e => e.Tolerance).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ApprovalReason).HasMaxLength(500);
        builder.Ignore(e => e.PorAprobar);
        builder.HasIndex(e => new { e.CashCountId, e.PaymentMeansId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_CashCountId_PaymentMeansId");
    }
}

/// <summary><c>RCD_CashCountDenominations</c>. (nuevo)</summary>
public class CashCountDenominationConfiguration : IEntityTypeConfiguration<CashCountDenomination>
{
    public const string Tabla = "RCD_CashCountDenominations";

    public void Configure(EntityTypeBuilder<CashCountDenomination> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashCountLine>().WithMany().HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashDenomination>().WithMany().HasForeignKey(e => e.CashDenominationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => e.CashCountLineId).HasDatabaseName($"IX_{Tabla}_CashCountLineId");
    }
}

/// <summary><c>RCD_CashCountTerminalBatches</c>. (nuevo)</summary>
public class CashCountTerminalBatchConfiguration : IEntityTypeConfiguration<CashCountTerminalBatch>
{
    public const string Tabla = "RCD_CashCountTerminalBatches";

    public void Configure(EntityTypeBuilder<CashCountTerminalBatch> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashCountLine>().WithMany().HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.CardTerminalId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.BatchNumber).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => e.CashCountLineId).HasDatabaseName($"IX_{Tabla}_CashCountLineId");
    }
}

/// <summary><c>RCD_CashCountReferences</c>: con el medio del recibo que cubre (<c>RCD_ReceiptMeans</c>). (nuevo)</summary>
public class CashCountReferenceConfiguration : IEntityTypeConfiguration<CashCountReference>
{
    public const string Tabla = "RCD_CashCountReferences";

    public void Configure(EntityTypeBuilder<CashCountReference> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashCountLine>().WithMany().HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReceiptMeans>().WithMany().HasForeignKey(e => e.ReceiptMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Reference).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => e.CashCountLineId).HasDatabaseName($"IX_{Tabla}_CashCountLineId");
        builder.HasIndex(e => e.ReceiptMeansId).HasDatabaseName($"IX_{Tabla}_ReceiptMeansId");
    }
}

/// <summary><c>RCD_CashMovements</c>: número único por tipo de documento. (nuevo)</summary>
public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public const string Tabla = "RCD_CashMovements";

    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<ReceiptDocumentType>().WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CounterpartSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vault>().WithMany().HasForeignKey(e => e.VaultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.FromMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.ToMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SecondCustodianUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(e => new { e.DocumentTypeId, e.Number }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_DocumentTypeId_Number");
        builder.HasIndex(e => e.CashSessionId).HasDatabaseName($"IX_{Tabla}_CashSessionId");
        builder.HasIndex(e => e.CounterpartSessionId).HasDatabaseName($"IX_{Tabla}_CounterpartSessionId");
        builder.HasIndex(e => e.ReceiptId).HasDatabaseName($"IX_{Tabla}_ReceiptId");
    }
}

/// <summary><c>RCD_CashMovementDenominations</c>. (nuevo)</summary>
public class CashMovementDenominationConfiguration : IEntityTypeConfiguration<CashMovementDenomination>
{
    public const string Tabla = "RCD_CashMovementDenominations";

    public void Configure(EntityTypeBuilder<CashMovementDenomination> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashMovement>().WithMany().HasForeignKey(e => e.CashMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashDenomination>().WithMany().HasForeignKey(e => e.CashDenominationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => e.CashMovementId).HasDatabaseName($"IX_{Tabla}_CashMovementId");
    }
}

/// <summary><c>RCD_CashSessionAdjustments</c>. (nuevo)</summary>
public class CashSessionAdjustmentConfiguration : IEntityTypeConfiguration<CashSessionAdjustment>
{
    public const string Tabla = "RCD_CashSessionAdjustments";

    public void Configure(EntityTypeBuilder<CashSessionAdjustment> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(e => e.Number).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => e.CashSessionId).HasDatabaseName($"IX_{Tabla}_CashSessionId");
    }
}

/// <summary><c>RCD_DayCloses</c>: un cierre por día del punto. (nuevo)</summary>
public class DayCloseConfiguration : IEntityTypeConfiguration<DayClose>
{
    public const string Tabla = "RCD_DayCloses";

    public void Configure(EntityTypeBuilder<DayClose> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionPointDay>().WithMany().HasForeignKey(e => e.CollectionPointDayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupervisorDelegation>().WithMany().HasForeignKey(e => e.SupervisorDelegationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Expected).Monto();
        builder.Property(e => e.Counted).Monto();
        builder.Property(e => e.Difference).Monto();
        builder.Property(e => e.VerificationCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.CollectionPointDayId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_CollectionPointDayId");
    }
}

/// <summary><c>RCD_DayCloseLines</c>. (nuevo)</summary>
public class DayCloseLineConfiguration : IEntityTypeConfiguration<DayCloseLine>
{
    public const string Tabla = "RCD_DayCloseLines";

    public void Configure(EntityTypeBuilder<DayCloseLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<DayClose>().WithMany().HasForeignKey(e => e.DayCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Expected).Monto();
        builder.Property(e => e.Counted).Monto();
        builder.Property(e => e.Difference).Monto();
        builder.HasIndex(e => e.DayCloseId).HasDatabaseName($"IX_{Tabla}_DayCloseId");
    }
}

/// <summary><c>RCD_DailySquares</c>: UK <c>(BranchId, BusinessDate)</c>. (nuevo)</summary>
public class DailySquareConfiguration : IEntityTypeConfiguration<DailySquare>
{
    public const string Tabla = "RCD_DailySquares";

    public void Configure(EntityTypeBuilder<DailySquare> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ExplainedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.CashInBoxes).Monto();
        builder.Property(e => e.CashInVault).Monto();
        builder.Property(e => e.CashInTransit).Monto();
        builder.Property(e => e.LedgerBalance).Monto();
        builder.Property(e => e.EmittedPendingBatch).Monto();
        builder.Property(e => e.Difference).Monto();
        builder.Property(e => e.Explanation).HasMaxLength(1000);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.BranchId, e.BusinessDate }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_BranchId_BusinessDate");
    }
}

/// <summary><c>RCD_CashierReceivables</c>. (nuevo)</summary>
public class CashierReceivableConfiguration : IEntityTypeConfiguration<CashierReceivable>
{
    public const string Tabla = "RCD_CashierReceivables";

    public void Configure(EntityTypeBuilder<CashierReceivable> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CashierPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashCountLine>().WithMany().HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Balance).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.CashCountLineId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_CashCountLineId");
        builder.HasIndex(e => e.CashierPersonId).HasDatabaseName($"IX_{Tabla}_CashierPersonId");
    }
}

/// <summary><c>RCD_CashSurpluses</c>. (nuevo)</summary>
public class CashSurplusConfiguration : IEntityTypeConfiguration<CashSurplus>
{
    public const string Tabla = "RCD_CashSurpluses";

    public void Configure(EntityTypeBuilder<CashSurplus> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashCountLine>().WithMany().HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.CashCountLineId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_CashCountLineId");
    }
}

/// <summary><c>RCD_VaultMovements</c>: número único. (nuevo)</summary>
public class VaultMovementConfiguration : IEntityTypeConfiguration<VaultMovement>
{
    public const string Tabla = "RCD_VaultMovements";

    public void Configure(EntityTypeBuilder<VaultMovement> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Vault>().WithMany().HasForeignKey(e => e.VaultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashMovement>().WithMany().HasForeignKey(e => e.CashMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CustodianUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SecondCustodianUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.BagSeal).HasMaxLength(40);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasIndex(e => e.Number).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => e.VaultId).HasDatabaseName($"IX_{Tabla}_VaultId");
        builder.HasIndex(e => e.CashMovementId).HasDatabaseName($"IX_{Tabla}_CashMovementId");
    }
}

/// <summary><c>RCD_VaultMovementDenominations</c>. (nuevo)</summary>
public class VaultMovementDenominationConfiguration : IEntityTypeConfiguration<VaultMovementDenomination>
{
    public const string Tabla = "RCD_VaultMovementDenominations";

    public void Configure(EntityTypeBuilder<VaultMovementDenomination> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<VaultMovement>().WithMany().HasForeignKey(e => e.VaultMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashDenomination>().WithMany().HasForeignKey(e => e.CashDenominationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => e.VaultMovementId).HasDatabaseName($"IX_{Tabla}_VaultMovementId");
    }
}

/// <summary><c>RCD_VaultDenominationBalances</c>: UK <c>(VaultId, CashDenominationId)</c>. (nuevo)</summary>
public class VaultDenominationBalanceConfiguration : IEntityTypeConfiguration<VaultDenominationBalance>
{
    public const string Tabla = "RCD_VaultDenominationBalances";

    public void Configure(EntityTypeBuilder<VaultDenominationBalance> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Vault>().WithMany().HasForeignKey(e => e.VaultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashDenomination>().WithMany().HasForeignKey(e => e.CashDenominationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => new { e.VaultId, e.CashDenominationId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_VaultId_DenominationId");
    }
}
