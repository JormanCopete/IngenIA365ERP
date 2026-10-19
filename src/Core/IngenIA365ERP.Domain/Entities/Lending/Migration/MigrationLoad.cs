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

    /// <summary>
    /// Feature 014 (T028; data-model §1.8, FR-116): de qué naturaleza es la carga —créditos, captaciones o aportes—. Un borrador, una aplicada y
    /// una activada <b>por naturaleza</b>. Toda carga de la 013 es de colocación (defecto).
    /// </summary>
    public LineNature Nature { get; set; } = LineNature.Placement;

    /// <summary>Feature 014: cuentas de la carga (captaciones o aportes); 0 en una de créditos.</summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Feature 014 (US9, T156; FR-118; api.md §16): las <b>cifras de referencia</b> que entrega la cooperativa para cotejar los totales cargados, como
    /// JSON <c>[{ lineCode, concept, amount, source }]</c>; las reemplaza entero <c>PUT …/reference-figures</c>. Nula en una carga de créditos.
    /// </summary>
    public string? ReferenceFiguresJson { get; set; }

    // ------------------------------------------------------------------------------------------------- comportamiento (US8) --

    /// <summary>Sólo un borrador recibe partes y se aplica (<c>Lending.Migration.LoadNotDraft</c>).</summary>
    public bool EsBorrador => Status == MigrationLoadStatus.Draft;

    /// <summary>
    /// La deja <c>Applied</c> (T0743): quién y cuándo, el plazo de la activación, los conteos y los totales. El cuadre y la conciliación se sellan
    /// aparte (<see cref="SellarCuadre"/>, <see cref="SellarConciliacion"/>). Sólo desde un borrador.
    /// </summary>
    public void Aplicar(int usuario, DateTime ahora, DateOnly plazo, int creditos, int castigados, int personas, string totalesJson, string resumenJson)
    {
        if (!EsBorrador) throw new InvalidOperationException("Sólo se aplica una carga en borrador.");
        Status = MigrationLoadStatus.Applied;
        AppliedByUserId = usuario;
        AppliedAt = ahora;
        ActivationDeadline = plazo;
        LoanCount = creditos;
        ChargedOffCount = castigados;
        PersonCount = personas;
        TotalsJson = totalesJson;
        ValidationSummaryJson = resumenJson;
    }

    /// <summary>La deja <c>Replaced</c> por otra (la reemplazada no se borra y queda como ensayo, FR-142).</summary>
    public void Reemplazar(int? porCarga, DateTime ahora)
    {
        if (Status is not (MigrationLoadStatus.Draft or MigrationLoadStatus.Applied))
            throw new InvalidOperationException("Sólo se reemplaza un borrador o una carga aplicada.");
        Status = MigrationLoadStatus.Replaced;
        ReplacedAt = ahora;
        if (porCarga is not null) ReplacedByLoadId = porCarga;
    }

    /// <summary>Sella la versión del cuadre con su instantánea y su huella; <paramref name="cuadrada"/>: sin diferencias o todas aceptadas.</summary>
    public void SellarCuadre(int version, string instantaneaJson, string huella, bool cuadrada)
    {
        CurrentCheckVersion = version;
        BalanceCheckJson = instantaneaJson;
        BalanceCheckSha256 = huella;
        IsBalanced = cuadrada;
    }

    /// <summary>Sella la huella de la conciliación de ventas de la versión vigente; <paramref name="conciliada"/>: sin diferencias o todas aceptadas.</summary>
    public void SellarConciliacion(string huella, bool conciliada)
    {
        SalesReconciliationSha256 = huella;
        SalesReconciled = conciliada;
    }
}
