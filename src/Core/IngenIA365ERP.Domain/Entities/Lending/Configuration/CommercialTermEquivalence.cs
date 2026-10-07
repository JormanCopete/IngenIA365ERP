using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CommercialTermEquivalences</c> (feature 013, T0210; data-model §1.9; FR-022, FR-050, T17): traduce a Cartera las
/// periodicidades y unidades de plazo que llegan de Comercial como texto. Versión propia de Cartera (hecho, C42) leída a la fecha
/// de la venta.
/// </summary>
public class CommercialTermEquivalence : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public CommercialTermKind Kind { get; init; }

    /// <summary>El texto que manda Comercial, en mayúsculas.</summary>
    public string CommercialCode { get; init; } = string.Empty;

    public RepaymentPeriodicity? Periodicity { get; init; }

    /// <summary>Días por unidad de plazo (días = 1, meses = 30, años = 360).</summary>
    public int? DaysPerUnit { get; init; }

    public DateOnly ValidFrom { get; init; }

    public int VersionNumber { get; init; }

    public DateTime RegisteredAt { get; init; }
}
