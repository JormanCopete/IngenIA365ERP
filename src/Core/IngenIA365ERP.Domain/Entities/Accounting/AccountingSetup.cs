using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Configuración contable de la empresa (feature 009, FR-003): una sola fila por cooperativa.
/// Catálogo elegido, nivel de movimiento (5 o 6), longitudes de los códigos de nivel 5 y 6,
/// grupo NIIF, sucursal principal, cuatro ojos y tolerancias. Catálogo, nivel y longitudes
/// sólo cambian mientras no exista ninguna auxiliar ni ningún movimiento (FR-004).
/// </summary>
public class AccountingSetup : AuditableEntity
{
    public int CatalogId { get; set; }
    public AccountCatalog? Catalog { get; set; }

    /// <summary>5 o 6: el nivel cuyas cuentas reciben movimientos.</summary>
    public byte MovementLevel { get; set; }

    public byte Level5Length { get; set; }
    public byte Level6Length { get; set; }

    /// <summary>1, 2 o 3 (NIIF plenas, NIIF para PYMES, microempresas).</summary>
    public byte NiifGroup { get; set; }

    public int FirstFiscalYear { get; set; }

    /// <summary>Cuenta de movimiento a la que el cierre lleva el resultado del ejercicio; puede fijarse después de iniciar.</summary>
    public int? ResultAccountId { get; set; }
    public ChartOfAccount? ResultAccount { get; set; }

    public int MainBranchId { get; set; }
    public Branch? MainBranch { get; set; }

    /// <summary>Quien contabiliza un comprobante manual debe ser distinto de quien lo registró.</summary>
    public bool FourEyes { get; set; }

    /// <summary>Días de tolerancia al proponer coincidencias por valor y fecha en la conciliación.</summary>
    public int ReconciliationDayTolerance { get; set; }

    /// <summary>Pesos de diferencia admitidos entre el valor de una línea de impuesto y base × tarifa.</summary>
    public decimal TaxTolerance { get; set; }

    /// <summary>El comprobante de apertura contabilizado y no reversado, si existe (FR-087).</summary>
    public long? OpeningDocumentId { get; set; }
    public AccountingDocument? OpeningDocument { get; set; }

    public DateTime InitializedAt { get; set; }
    public string InitializedBy { get; set; } = string.Empty;
}
