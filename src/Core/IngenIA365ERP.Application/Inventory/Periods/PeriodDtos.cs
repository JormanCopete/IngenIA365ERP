using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Periods;

// Los DTOs de /api/inventory/periods (feature 012, T289, T290; contracts/api.md §13.4). (nuevos)

/// <summary><c>GET /periods?year=</c>: los meses desde el de <c>startDate</c> hasta el actual.</summary>
public sealed record InventoryPeriodsDto(DateOnly? StartDate, DateOnly? LastClosedDate, IReadOnlyList<InventoryPeriodDto> Periods);

/// <summary>Un mes con su estado; los datos de cierre y reapertura sólo si los tuvo.</summary>
public sealed record InventoryPeriodDto(
    int Year,
    int Month,
    InventoryPeriodStatus Status,
    DateTime? ClosedAt,
    UsuarioDto? ClosedBy,
    DateTime? ReopenedAt,
    UsuarioDto? ReopenedBy,
    string? ReopenReason,
    UnbilledShipmentsAcceptedDto? UnbilledShipmentsAccepted,
    int? ClosingVersion);

/// <summary>Las remisiones sin facturar aceptadas al cerrar (I6).</summary>
public sealed record UnbilledShipmentsAcceptedDto(UsuarioDto By, DateTime At, string Reason, int Count, decimal Value);

/// <summary><c>GET /{year}/{month}/close</c>: la vista previa del cierre.</summary>
public sealed record PeriodCloseCheckDto(
    int Year,
    int Month,
    bool CanClose,
    CloseBlockersDto Blockers,
    CloseWarningsDto Warnings,
    IReadOnlyList<UnbilledShipmentDto> UnbilledShipments,
    decimal UnbilledTotal);

/// <summary>Lo que impide cerrar: conteos abiertos con foto (US11), otro mes por cerrar antes o un mes que no terminó.</summary>
public sealed record CloseBlockersDto(
    IReadOnlyList<InventoryErrors.ConteoAbierto> OpenCounts,
    InventoryErrors.MesDeInventario? NotNext,
    DateOnly? NotEnded)
{
    public bool Any => OpenCounts.Count > 0 || NotNext is not null || NotEnded is not null;
}

/// <summary>Lo que avisa sin bloquear: con <c>acknowledgeWarnings</c> se cierra y queda en <c>CloseWarningsJson</c>.</summary>
public sealed record CloseWarningsDto(int Drafts, int PendingApproval, IReadOnlyList<UnresolvedTransferDto> UnresolvedTransfers, PendingMessagesDto Messages)
{
    public bool Any => Drafts > 0 || PendingApproval > 0 || UnresolvedTransfers.Count > 0 || Messages.Total > 0;
}

/// <summary>Un despacho de traslado con fecha en o antes del mes que no tiene recepción.</summary>
public sealed record UnresolvedTransferDto(Guid DispatchPublicId, string? DisplayNumber, decimal PendingBase);

/// <summary>Entregas de mensajes con fecha en el mes que no se procesaron.</summary>
public sealed record PendingMessagesDto(int Pending, int InBatch, int Rejected)
{
    public int Total => Pending + InBatch + Rejected;
}

/// <summary>Una remisión sin facturar (I6): valor al precio del pedido o de la lista vigente.</summary>
public sealed record UnbilledShipmentDto(Guid DocumentPublicId, string? DisplayNumber, string Customer, decimal Value, string ValueSource);

/// <summary><c>POST /{year}/{month}/close</c>: el valorizado fijado por grupo y bodega (valores nulos sin <c>Inventory.Costs.Read</c>).</summary>
public sealed record ClosePeriodResultDto(
    int Year,
    int Month,
    DateTime ClosedAt,
    int ClosingVersion,
    IReadOnlyList<PeriodValuationDto> Valuation,
    decimal? Total,
    Guid MessagePublicId,
    Guid? BatchPublicId);

/// <summary>Una línea del valorizado por grupo contable y bodega.</summary>
public sealed record PeriodValuationDto(CodigoYNombreDto AccountingGroup, BodegaDelValorizadoDto Warehouse, decimal Quantity, decimal? Value);

public sealed record CodigoYNombreDto(string Code, string Name);

public sealed record BodegaDelValorizadoDto(Guid PublicId, string Code);

/// <summary><c>POST /{year}/{month}/reopen</c>.</summary>
public sealed record ReopenPeriodResultDto(int Year, int Month, DateTime ReopenedAt, int SupersededClosingVersion, Guid MessagePublicId);
