using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRiskData</c> (feature 013, K1; data-model §29.3, parte <c>risk-data</c>; plantillas.md §5.3.4): el dato de riesgo al corte de
/// <b>un crédito</b> de la parte <c>loans</c> (una fila por crédito). Hecho sin diff de auditoría. Al activar forma el cierre base: sus líneas salen
/// de cada fila y el deterioro general es la suma de <see cref="GeneralImpairment"/> por oficina, modalidad y sub-segmento del crédito (FR-140).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationRiskDataRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El crédito de la parte <c>loans</c> (<c>numeroDeCredito</c>); llave de la parte.</summary>
    public string SourceLoanNumber { get; init; } = string.Empty;

    /// <summary>Días de mora al corte (<c>diasDeMora</c>).</summary>
    public int DaysPastDue { get; init; }

    public RatingCategory Rating { get; init; }

    public RatingSubcategory Subcategory { get; init; } = RatingSubcategory.None;

    /// <summary>Causa de la calificación (<c>causa</c>); nula si la fila no la trae.</summary>
    public RatingCause? Cause { get; init; }

    public decimal PrincipalImpairment { get; init; }

    public decimal InterestImpairment { get; init; }

    public decimal OtherImpairment { get; init; }

    /// <summary>La parte del crédito en el deterioro general (<c>deterioroGeneral</c>); el cierre base la suma por dimensión.</summary>
    public decimal GeneralImpairment { get; init; }

    /// <summary>Fecha de ingreso a la categoría E (<c>fechaDeIngresoAE</c>); con E. Los meses en E salen de ella y del corte.</summary>
    public DateOnly? EnteredEOn { get; init; }

    /// <summary>El crédito ya había suspendido la causación (<c>enSuspenso</c>).</summary>
    public bool IsSuspended { get; init; }
}
