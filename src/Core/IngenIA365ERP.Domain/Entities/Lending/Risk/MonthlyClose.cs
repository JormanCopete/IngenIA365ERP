using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_MonthlyCloses</c> (feature 013, US9, T0696; data-model §23.1; FR-107, FR-140, D12, SC-006; T47, T49): el encabezado del cierre
/// mensual y del cierre base. Nace en la <b>transacción final</b> de su corrida (el base, en la transacción de la activación) y nunca cambia
/// (<see cref="IHechoInmutable"/>, R61): no tiene estado guardado —<see cref="MonthlyCloseStatus.Reopened"/> se deriva de su fila en
/// <c>LND_MonthlyCloseReopenings</c>— ni <c>SupersededAt</c>; volver a cerrar un mes reabierto es otra fila con <c>Version + 1</c> y
/// <see cref="SupersedesCloseId"/>. (nuevo)
/// </summary>
public class MonthlyClose : AuditableEntity, IHechoInmutable
{
    public MonthlyCloseKind Kind { get; init; }

    /// <summary>En el base, el mes del corte.</summary>
    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    /// <summary>1 al primer cierre del mes; volver a cerrar tras reabrir crea la siguiente.</summary>
    public int Version { get; init; }

    /// <summary>Mensual: el último día del mes. Base: la fecha de corte (no tiene que ser fin de mes, T49).</summary>
    public DateOnly CloseDate { get; init; }

    /// <summary>La corrida <c>MonthlyClose</c>; nula en el base.</summary>
    public int? ProcessRunId { get; init; }

    /// <summary>Sólo en el base.</summary>
    public int? ActivationId { get; init; }

    /// <summary>El cierre vigente con el que se compara: el mensual anterior o, en el primero tras activar, el base.</summary>
    public int? PreviousCloseId { get; init; }

    /// <summary>Con <see cref="Version"/> &gt; 1, el cierre reabierto al que reemplaza.</summary>
    public int? SupersedesCloseId { get; init; }

    /// <summary>R sellado (T47).</summary>
    public DateTime InformationCutoffAt { get; init; }

    /// <summary><c>Sequence</c> de la última réplica de valores nacionales usada.</summary>
    public long NationalValuesWatermark { get; init; }

    /// <summary>Cada documento normativo usado: <c>PublicId</c>, clase, código, vigencia y SHA-256.</summary>
    public string DocumentsUsedJson { get; init; } = "[]";

    /// <summary>La política SARC validada vigente en <see cref="CloseDate"/> conocida en R.</summary>
    public int? RiskPolicyVersionId { get; init; }

    /// <summary>Por segmento: modelo, fundamento y marca (<c>SelectorDeModeloDeDeterioro</c>).</summary>
    public string ModelBySegmentJson { get; init; } = "[]";

    /// <summary>D28, FR-105.</summary>
    public bool ObligatoryModelUnavailable { get; init; }

    /// <summary>Se usaron valores o tablas «pendiente de validar» (T41, C76).</summary>
    public bool UnvalidatedParameters { get; init; }

    public string? UnvalidatedParametersJson { get; init; }

    /// <summary>Créditos sin historial completo (K6a, FR-105).</summary>
    public bool IncompleteHistory { get; init; }

    public string? IncompleteHistoryJson { get; init; }

    public int LoanCount { get; init; }

    public decimal TotalPrincipal { get; init; }

    public decimal TotalInterest { get; init; }

    public decimal TotalIndividualImpairment { get; init; }

    public decimal TotalGeneralImpairment { get; init; }

    /// <summary>Totales por modalidad, sub-segmento, categoría y sucursal.</summary>
    public string TotalsJson { get; init; } = "{}";

    /// <summary>Huella de las líneas, en orden de <c>LoanId</c>.</summary>
    public string LinesSha256 { get; init; } = string.Empty;

    /// <summary>Quien ordenó; nulo si fue programado o es el base sin persona.</summary>
    public int? OrderedByUserId { get; init; }

    public DateTime OrderedAt { get; init; }

    /// <summary>El instante de la transacción final.</summary>
    public DateTime ClosedAt { get; init; }

    /// <summary>El estado, derivado de la reapertura (R61): nunca se guarda.</summary>
    public static MonthlyCloseStatus EstadoCon(bool tieneReapertura) => tieneReapertura ? MonthlyCloseStatus.Reopened : MonthlyCloseStatus.Closed;

    /// <summary>El último día del mes <paramref name="anio"/>-<paramref name="mes"/>.</summary>
    public static DateOnly UltimoDiaDe(int anio, int mes) => new DateOnly(anio, mes, 1).AddMonths(1).AddDays(-1);
}
