using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRiskData</c> (feature 013, K1; data-model §29.3, parte <c>risk-data</c>): dato de riesgo al corte, de un crédito
/// (<c>RecordKind = LOAN</c>) o del deterioro general por dimensiones (<c>GENERAL</c>). Hecho sin diff de auditoría. Al activar
/// forma el cierre base: encabezado, líneas y deterioro general (§23). Las columnas de cada clase son nulas en la otra.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationRiskDataRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary><c>LOAN</c> o <c>GENERAL</c>.</summary>
    public string RecordKind { get; init; } = string.Empty;

    // ------------------------------------------------------------------------------------------------- crédito (LOAN) --

    public string? SourceLoanNumber { get; init; }

    public int? DaysPastDue { get; init; }

    public RatingCategory? Rating { get; init; }

    public RatingSubcategory? Subcategory { get; init; }

    public int? MonthsInE { get; init; }

    public bool? IsSuspended { get; init; }

    public decimal? PrincipalImpairment { get; init; }

    public decimal? InterestImpairment { get; init; }

    public decimal? OtherImpairment { get; init; }

    /// <summary>Porcentaje de deterioro como fracción.</summary>
    public decimal? ImpairmentPercentage { get; init; }

    // ----------------------------------------------------------------------------------------------- general (GENERAL) --

    public Guid? OfficePublicId { get; init; }

    public string? ModalityCode { get; init; }

    public string? SubsegmentCode { get; init; }

    public decimal? GeneralImpairment { get; init; }
}
