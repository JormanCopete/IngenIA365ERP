using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationArrearsHistory</c> (feature 013, K1; data-model §29.3, parte <c>arrears-history</c>): la mora de un mes de un
/// crédito de origen. Hecho de alto volumen (<see cref="AuditableEntityLong"/>, sin diff de auditoría). Al activar nace
/// <c>LND_LoanArrearsHistory</c> con origen <c>Migration</c> (obligatorio sólo en los segmentos cubiertos de una obligada, FR-139).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationArrearsHistoryRow : AuditableEntityLong, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    public int MaxDaysPastDue { get; init; }

    public int? DaysPastDueAtMonthEnd { get; init; }
}
