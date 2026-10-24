using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Compliance;

/// <summary>
/// <c>RCD_CashTransactionReports</c> (feature 015, R1, T0389; FR-096; data-model §7.3; molde <c>LND_RegulatoryFiles</c>): una generación del reporte de transacciones a la
/// UIAF de un mes. Sella el alcance, el estado de aplicación del período y el layout (con su cotejo). Con bloqueantes queda <c>Validated</c> sin archivo; regenerar deja la
/// anterior <c>Superseded</c>; marcar cargado con radicado cierra el período. Sólo cambian el estado y lo del cargue. (nuevo)
/// </summary>
public class CashTransactionReport : AuditableEntity
{
    public int PeriodYear { get; set; }

    public int PeriodMonth { get; set; }

    public int Version { get; set; }

    public CashTransactionReportScope Scope { get; set; }

    public CashTransactionReportApplication ApplicationState { get; set; }

    public string LayoutCode { get; set; } = string.Empty;

    public string LayoutVersion { get; set; } = string.Empty;

    public bool LayoutVerified { get; set; }

    public CashTransactionReportStatus Status { get; set; }

    public Guid? FileAttachmentPublicId { get; set; }

    public string? FileName { get; set; }

    public string? Sha256 { get; set; }

    public string? UploadReference { get; set; }

    public DateTime? UploadedAt { get; set; }

    public int? UploadedByUserId { get; set; }

    public Guid? UploadSupportAttachmentPublicId { get; set; }

    public string IssuesJson { get; set; } = "[]";

    public int BlockingIssues { get; set; }

    public int Warnings { get; set; }

    public bool IsAbsenceReport { get; set; }

    public int Lines { get; set; }

    public decimal Total { get; set; }

    public int GeneratedByUserId { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? SupersededAt { get; set; }
}

/// <summary>
/// <c>RCD_CashTransactionReportLines</c> (H; data-model §7.3): una línea del reporte —una transacción individual sobre el umbral o las múltiples del mes de una persona— con
/// la persona, el valor, cuántas transacciones y el documento (o los documentos). (nuevo)
/// </summary>
public class CashTransactionReportLine : AuditableEntity, IHechoInmutable
{
    public int CashTransactionReportId { get; set; }

    public int LineNumber { get; set; }

    public CashTransactionReportLineKind Kind { get; set; }

    public int PersonId { get; set; }

    public decimal Amount { get; set; }

    public int TransactionCount { get; set; }

    public DateOnly? OccurredOn { get; set; }

    public string? DocumentNumber { get; set; }

    /// <summary>Los documentos de la línea (en las múltiples, todos los del mes), en JSON.</summary>
    public string DocumentsJson { get; set; } = "[]";

    /// <summary>De dónde salió la línea: <c>Receipts</c>, <c>PointOfSale</c> o los dos (registro único, FR-204).</summary>
    public string Source { get; set; } = "Receipts";
}
