using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Activo fijo o cargo diferido (feature 009, FR-076..FR-082). Método de línea recta con valor
/// residual; el calendario de cuotas se genera al registrar y se regenera sólo hacia adelante
/// al cambiar vida útil o residual. La corrida mensual contabiliza las cuotas pendientes del
/// período por el contrato único; la baja genera su propio comprobante.
/// </summary>
public class FixedAsset : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FixedAssetKind Kind { get; set; }

    public int AssetAccountId { get; set; }
    public ChartOfAccount? AssetAccount { get; set; }
    public int? AccumulatedAccountId { get; set; }
    public ChartOfAccount? AccumulatedAccount { get; set; }
    public int ExpenseAccountId { get; set; }
    public ChartOfAccount? ExpenseAccount { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public int? SupplierPersonId { get; set; }
    public Person? SupplierPerson { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public string? PurchaseDocument { get; set; }
    public decimal Cost { get; set; }
    public decimal ResidualValue { get; set; }
    public int LifeMonths { get; set; }
    public DateOnly StartDate { get; set; }

    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Active;
    public DateOnly? RetiredAt { get; set; }
    public string? RetireReason { get; set; }
    public long? RetireDocumentId { get; set; }
    public AccountingDocument? RetireDocument { get; set; }
    public int? RetireCounterAccountId { get; set; }

    public ICollection<FixedAssetInstallment> Installments { get; set; } = [];

    public decimal ValorDepreciable => Cost - ResidualValue;
}

/// <summary>Cuota de depreciación o amortización de un activo en un período; única por activo y período.</summary>
public class FixedAssetInstallment : AuditableEntityLong
{
    public int AssetId { get; set; }
    public FixedAsset? Asset { get; set; }
    public int PeriodId { get; set; }
    public AccountingPeriod? Period { get; set; }
    public decimal Amount { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;
    public long? DocumentId { get; set; }
    public AccountingDocument? Document { get; set; }
}

/// <summary>Corrida mensual de depreciación y amortización: una por período (restricción única), con su comprobante y su reversión.</summary>
public class AssetRun : AuditableEntity
{
    public int PeriodId { get; set; }
    public AccountingPeriod? Period { get; set; }
    public long DocumentId { get; set; }
    public AccountingDocument? Document { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string ExecutedBy { get; set; } = string.Empty;
    public AssetRunStatus Status { get; set; } = AssetRunStatus.Posted;
    public long? ReversalDocumentId { get; set; }
    public AccountingDocument? ReversalDocument { get; set; }
}
