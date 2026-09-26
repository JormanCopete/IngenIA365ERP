using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Transfers;

// Las formas de los traslados (feature 012, US10, T374; contracts/api.md §11). Sólo PublicId.

/// <summary>Los estados derivados de un traslado (texto; api.md §11). (nuevo)</summary>
public static class EstadosDeTraslado
{
    public const string Borrador = "Draft";
    public const string EnAprobacion = "PendingApproval";
    public const string EnTransito = "InTransit";
    public const string Recibido = "Received";
    public const string RecibidoConDiferencias = "ReceivedWithDiscrepancies";
    public const string Anulado = "Voided";

    public static readonly IReadOnlyList<string> Todos = [Borrador, EnAprobacion, EnTransito, Recibido, RecibidoConDiferencias, Anulado];
}

/// <summary>Una fila de la lista de traslados: el despacho con lo recibido y las diferencias pendientes; el valor con <c>Costs.Read</c>.</summary>
public sealed record TransferSummaryDto(
    Guid DispatchPublicId,
    string? DisplayNumber,
    string State,
    DateOnly OperationDate,
    ReferenciaDto? Origin,
    ReferenciaDto? Destination,
    ReferenciaDto? Transit,
    int Lines,
    decimal QuantityDispatched,
    decimal QuantityReceived,
    int PendingDiscrepancies,
    decimal? Value);

/// <summary>Una línea del traslado: lo despachado, lo recibido, el faltante, el sobrante y lo que sigue en tránsito (unidad base).</summary>
public sealed record TransferLineDto(
    Guid DispatchLinePublicId,
    ReferenciaDto Product,
    UnidadDto Unit,
    decimal DispatchedBase,
    decimal ReceivedBase,
    decimal ShortageBase,
    decimal SurplusBase,
    decimal PendingBase);

/// <summary>El traslado con quien lo pidió: el despacho y sus recepciones como documentos, las líneas y las diferencias.</summary>
public sealed record TransferDto(
    InventoryDocumentDto Dispatch,
    IReadOnlyList<InventoryDocumentDto> Receipts,
    IReadOnlyList<TransferLineDto> Lines,
    IReadOnlyList<TransferDiscrepancyDto> Discrepancies);

/// <summary>El despacho de una diferencia.</summary>
public sealed record TransferRefDto(Guid DispatchPublicId, string? DisplayNumber);

/// <summary>El documento que resuelve una diferencia.</summary>
public sealed record ResolvingDocumentDto(Guid PublicId, DocumentClass Class, string? DisplayNumber, DocumentStatus Status);

/// <summary>
/// Una diferencia de traslado (§11). <see cref="State"/>: <c>Pending</c>, <c>InApproval</c> o <c>Resolved</c>;
/// <see cref="PendingBase"/> lo que falta resolver (nuevo: una aprobación parcial resuelve sólo su parte).
/// </summary>
public sealed record TransferDiscrepancyDto(
    Guid PublicId,
    TransferDiscrepancyKind Kind,
    TransferRefDto Transfer,
    ReferenciaDto Product,
    decimal QuantityBase,
    decimal PendingBase,
    decimal? Value,
    string State,
    TransferDiscrepancyResolution? Resolution,
    ResolvingDocumentDto? ResolvingDocument,
    Guid? ApprovalRequestPublicId,
    ReferenciaDto? DestinationWarehouse,
    DateTime CreatedAt);

/// <summary>Una diferencia que dejó la recepción.</summary>
public sealed record DiferenciaCreadaDto(Guid DiscrepancyPublicId, ReferenciaDto Product, decimal QuantityBase);

/// <summary>La respuesta de <c>POST /transfers/{id}/receive</c> (§11).</summary>
public sealed record ReceiveTransferResultDto(
    Guid ReceiptPublicId,
    string? DisplayNumber,
    DateOnly OperationDate,
    DocumentStatus Status,
    IReadOnlyList<DiferenciaCreadaDto> Shortages,
    IReadOnlyList<DiferenciaCreadaDto> Surpluses,
    IReadOnlyList<MensajeEmitidoDto>? Messages);

/// <summary>La respuesta de <c>POST /transfers/discrepancies/{id}/resolve</c> (§11).</summary>
public sealed record ResolveTransferDiscrepancyResultDto(
    Guid DiscrepancyPublicId,
    TransferDiscrepancyResolution Resolution,
    Guid DocumentPublicId,
    DocumentClass DocumentClass,
    DocumentStatus Status,
    Guid? ApprovalRequestPublicId);

/// <summary>Una bodega a la que se puede trasladar (<c>purpose=TransferDestination</c>, §11, §17.2): sin existencias ni valores.</summary>
public sealed record TransferDestinationDto(Guid PublicId, string Code, string Name, Warehouses.BranchRefDto Branch);
