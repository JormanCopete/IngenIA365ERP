using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Sessions;

/// <summary>
/// <c>RCD_CollectionPointDays</c> (feature 015, T0089; FR-033, FR-184; data-model §2.1): el día de operación de un punto, abierto con la primera sesión del día
/// y cerrado una sola vez cuando todas sus sesiones están cerradas y sin diferencias por aprobar. Nivel 2 del cerrojo (compartido; exclusivo al cerrar).
/// (nuevo)
/// </summary>
public class CollectionPointDay : AuditableEntity
{
    public int CollectionPointId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public CollectionPointDayStatus Status { get; set; } = CollectionPointDayStatus.Open;

    public int? ClosedByUserId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? DayCloseReportAttachmentPublicId { get; set; }
}

/// <summary>
/// <c>RCD_CashSessions</c> (feature 015, T0089; R-14; FR-022, FR-029 a FR-034; data-model §2.2, §19.2): la sesión de un cajero en una caja. <b>Sella al
/// abrir</b> (<see cref="SealedSettingsJson"/>) el modo de base, los topes, el arqueo ciego, las tolerancias y los medios del punto, que no cambian aunque
/// cambie la caja (US1-3). Nivel 3 del cerrojo (exclusivo). Un <see cref="CashSessionState.Closed"/> no se reabre: se corrige con un ajuste. Documento con
/// estado: cambian <see cref="Status"/>, <see cref="ExpectedByMeansJson"/> (proyección), el cierre y la marca de olvidada. (nuevo)
/// </summary>
public class CashSession : AuditableEntity
{
    public int CashBoxId { get; set; }

    /// <summary><c>SEC_Users.Id</c> del cajero.</summary>
    public int CashierUserId { get; set; }

    public int CollectionPointDayId { get; set; }

    public CashSessionState Status { get; set; } = CashSessionState.Open;

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>La base contada por denominaciones al abrir (Monto).</summary>
    public decimal OpeningBase { get; set; }

    /// <summary>Lo sellado al abrir (<c>ValoresSelladosDeLaSesion</c> en JSON).</summary>
    public string SealedSettingsJson { get; set; } = "{}";

    /// <summary>La proyección de lo esperado por medio; se recalcula en cada documento y se verifica al cerrar con el arqueo.</summary>
    public string ExpectedByMeansJson { get; set; } = "[]";

    /// <summary>La sesión cruzó la hora de corte abierta (FR-034).</summary>
    public bool IsForgotten { get; set; }

    /// <summary>Quien la cerró: el cajero o, en una olvidada, el supervisor.</summary>
    public int? ClosedByUserId { get; set; }

    /// <summary>El testigo del arqueo diferido de una sesión olvidada.</summary>
    public int? WitnessUserId { get; set; }

    /// <summary>Las sesiones que reciben operaciones: abiertas (no olvidadas).</summary>
    public bool Recibe => Status == CashSessionState.Open;

    /// <summary>Las que ocupan la caja (índice único filtrado de data-model §2.2).</summary>
    public static readonly CashSessionState[] Vivas = [CashSessionState.Open, CashSessionState.Closing, CashSessionState.ForgottenOpen];
}
