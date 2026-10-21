using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_LoanArrearsHistory</c> (feature 013, US9, T0696; data-model §23.5; FR-105, FR-139, FR-140; T49): la mora de un mes de un crédito y de
/// dónde sale —la migración, una carga complementaria (K6a) o un cierre mensual—. Las de un cierre nacen con sus líneas, enlazadas a la corrida,
/// y sólo cuentan si el cierre existe y está vigente; <c>HistorialDeMora.Construir</c> elige por mes la fuente que manda. Hecho sin diff de
/// auditoría (C46). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class LoanArrearsHistoryEntry : AuditableEntity, IHechoInmutable
{
    public int LoanId { get; init; }

    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    public int MaxDaysPastDue { get; init; }

    public int? DaysPastDueAtMonthEnd { get; init; }

    public ArrearsHistoryOrigin Origin { get; init; }

    public int? MigrationLoadId { get; init; }

    /// <summary>La carga complementaria (<c>LND_ArrearsHistoryLoads</c>, K6a); su FK nace con <c>CarteraPerdidaEsperada</c>.</summary>
    public int? ArrearsHistoryLoadId { get; init; }

    public int? ProcessRunId { get; init; }

    /// <summary>Historial migrado de un crédito novado o refinanciado que no nace en Cartera.</summary>
    public string? PredecessorLoanNumber { get; init; }
}
