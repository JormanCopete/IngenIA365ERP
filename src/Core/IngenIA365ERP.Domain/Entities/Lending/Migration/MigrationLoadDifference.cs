using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoadDifferences</c> (feature 013, K1; data-model §29.4; FR-141, FR-143, D17): una diferencia del cuadre o de la
/// conciliación de ventas de una versión de la carga. Hecho (<see cref="IHechoInmutable"/>): si al activar el resultado cambia,
/// nace <c>CheckVersion + 1</c> con sus filas. La aceptación (motivo y punto de acta) es de escritura única.
/// </summary>
public class MigrationLoadDifference : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadId { get; init; }

    public int CheckVersion { get; init; }

    public MigrationDifferenceKind Kind { get; init; }

    /// <summary>Llave de la dimensión; única por carga y versión.</summary>
    public string DimensionKey { get; init; } = string.Empty;

    /// <summary><c>BalanceCheck</c>: capital, intereses, suspenso, deterioro individual, general, anticipos, saldos a favor, castigada.</summary>
    public string? ConceptFamily { get; init; }

    public string? ModalityCode { get; init; }

    public string? SubsegmentCode { get; init; }

    public RatingCategory? RiskCategory { get; init; }

    public int? BranchId { get; init; }

    /// <summary>Conjunto que devolvió Contabilidad; sólo para mostrar (Cartera no resuelve cuentas, FR-129).</summary>
    public string? AccountSetKey { get; init; }

    /// <summary>Las cuentas del conjunto, sólo para mostrar.</summary>
    public string? AccountSetDisplay { get; init; }

    /// <summary>Diferencias de venta. El enlace con FK va de la conciliación a la diferencia, no al revés.</summary>
    public Guid? CreditPaymentPublicId { get; init; }

    public string? InvoiceNumber { get; init; }

    /// <summary>En ventas: el saldo de la venta.</summary>
    public decimal LendingAmount { get; init; }

    /// <summary>En ventas: 0.</summary>
    public decimal AccountingAmount { get; init; }

    public decimal Difference { get; init; }

    [EscrituraUnica]
    public DateTime? AcceptedAt { get; set; }

    [EscrituraUnica]
    public int? AcceptedByUserId { get; set; }

    [EscrituraUnica]
    public string? AcceptanceReason { get; set; }

    /// <summary>Punto de acta que respalda la aceptación (D17, T64).</summary>
    [EscrituraUnica]
    public int? SessionItemId { get; set; }
}
