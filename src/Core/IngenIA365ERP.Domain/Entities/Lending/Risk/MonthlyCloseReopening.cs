using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_MonthlyCloseReopenings</c> (feature 013, US9, T0696; data-model §23.4; FR-107, D12; T47, C58): la reapertura de un cierre, que la
/// inserta la corrida <c>CloseReopen</c> en una transacción con <c>LND_Setup</c> exclusivo. Un cierre se reabre una vez (único por
/// <see cref="MonthlyCloseId"/>); el encabezado y las líneas del cierre no se tocan. Hecho. (nuevo)
/// </summary>
public class MonthlyCloseReopening : AuditableEntity, IHechoInmutable
{
    public int MonthlyCloseId { get; init; }

    /// <summary>La corrida <c>CloseReopen</c>.</summary>
    public int ProcessRunId { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Quien ordenó, con <c>Lending.MonthlyClose.Reopen</c>.</summary>
    public int ReopenedByUserId { get; init; }

    /// <summary>El instante de la transacción.</summary>
    public DateTime ReopenedAt { get; init; }

    /// <summary>Lo que respondió <c>EstadoDelPeriodoAsync</c>: la prueba de que el período seguía abierto.</summary>
    public string AccountingPeriodStatusJson { get; init; } = "{}";

    public int ReversalPartCount { get; init; }
}
