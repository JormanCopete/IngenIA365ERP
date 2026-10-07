using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoads</c> (feature 013, K1; data-model §29.1; FR-139–FR-143, SC-013, T83): una carga de migración con su corte.
/// No es hecho: cambia de estado (<see cref="MigrationLoadStatus"/>) y guarda la versión vigente del cuadre y la conciliación.
/// Aplicarla <b>sólo guarda la carga</b>; nada nace en el libro hasta la activación (§30). Hay a lo sumo un borrador, una
/// aplicada y una activada por cooperativa.
/// </summary>
public class MigrationLoad : AuditableEntity
{
    /// <summary>Consecutivo de cargas; único.</summary>
    public int Number { get; set; }

    /// <summary>Corte; la activación será el día siguiente.</summary>
    public DateOnly CutoffDate { get; set; }

    public MigrationLoadStatus Status { get; set; } = MigrationLoadStatus.Draft;

    public string? Description { get; set; }

    public int CreatedByUserId { get; set; }

    public int? AppliedByUserId { get; set; }

    public DateTime? AppliedAt { get; set; }

    /// <summary>Último día para registrar la activación: corte + <c>Pagos.DiasMaximosFechaValor</c>, sellado al aplicar (FR-142).</summary>
    public DateOnly? ActivationDeadline { get; set; }

    /// <summary>Versión vigente del cuadre y la conciliación; sube al recalcular con otro resultado.</summary>
    public int CurrentCheckVersion { get; set; }

    /// <summary>JSON: instantánea del cuadre por conjunto (lo que respondió <c>SaldosDeCuentasMapeadasAsync</c>).</summary>
    public string? BalanceCheckJson { get; set; }

    public string? BalanceCheckSha256 { get; set; }

    /// <summary>Huella de las filas de <see cref="MigrationSaleReconciliation"/> de la versión vigente.</summary>
    public string? SalesReconciliationSha256 { get; set; }

    /// <summary>Sin diferencias de cuadre, o todas aceptadas.</summary>
    public bool IsBalanced { get; set; }

    /// <summary>Sin diferencias de ventas, o todas aceptadas.</summary>
    public bool SalesReconciled { get; set; }

    /// <summary>JSON: resultado de la validación entre partes y avisos.</summary>
    public string? ValidationSummaryJson { get; set; }

    public int LoanCount { get; set; }

    public int ChargedOffCount { get; set; }

    public int PersonCount { get; set; }

    /// <summary>JSON: capital, intereses, suspenso, anticipos, saldos a favor y deterioro, por modalidad y categoría.</summary>
    public string? TotalsJson { get; set; }

    /// <summary>La carga que reemplazó a ésta (autorreferencia); la reemplazada no se borra y sirve de ensayo.</summary>
    public int? ReplacedByLoadId { get; set; }

    public DateTime? ReplacedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }
}
