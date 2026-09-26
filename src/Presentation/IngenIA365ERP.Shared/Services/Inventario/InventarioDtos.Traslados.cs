namespace IngenIA365ERP.Shared.Services.Inventario;

// Espejos de los traslados (feature 012, US10, T379; contracts/api.md §11). Los enums viajan como número (Kind, Resolution) y los
// estados derivados como texto (State); la pantalla los traduce con TextosDeInventario.

/// <summary>Los filtros de la lista de traslados: <c>State</c> es uno de <see cref="TextosDeInventario.EstadosDeTraslado"/>.</summary>
public sealed record FiltroDeTraslados
{
    public string? State { get; init; }
    public Guid? OriginWarehousePublicId { get; init; }
    public Guid? DestinationWarehousePublicId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary><c>TransferSummaryDto</c>: el despacho con su estado derivado; <see cref="Value"/> sólo con <c>Inventory.Costs.Read</c>.</summary>
public sealed record ResumenDeTrasladoDto
{
    public Guid DispatchPublicId { get; init; }
    public string? DisplayNumber { get; init; }
    public string State { get; init; } = "";
    public DateOnly OperationDate { get; init; }
    public ReferenciaDeInventarioDto? Origin { get; init; }
    public ReferenciaDeInventarioDto? Destination { get; init; }
    public ReferenciaDeInventarioDto? Transit { get; init; }
    public int Lines { get; init; }
    public decimal QuantityDispatched { get; init; }
    public decimal QuantityReceived { get; init; }
    public int PendingDiscrepancies { get; init; }
    public decimal? Value { get; init; }
}

/// <summary><c>TransferLineDto</c>: despachado, recibido, faltante, sobrante y lo que sigue en tránsito, en unidad base.</summary>
public sealed record LineaDeTrasladoDto
{
    public Guid DispatchLinePublicId { get; init; }
    public ReferenciaDeInventarioDto Product { get; init; } = new(Guid.Empty, "", "");
    public UnidadDeLineaDto Unit { get; init; } = new(Guid.Empty, "");
    public decimal DispatchedBase { get; init; }
    public decimal ReceivedBase { get; init; }
    public decimal ShortageBase { get; init; }
    public decimal SurplusBase { get; init; }
    public decimal PendingBase { get; init; }
}

/// <summary>El despacho de una diferencia.</summary>
public sealed record TrasladoDeLaDiferenciaDto(Guid DispatchPublicId, string? DisplayNumber);

/// <summary>El documento que resuelve una diferencia (<c>Class</c> y <c>Status</c> como número).</summary>
public sealed record DocumentoQueResuelveDto(Guid PublicId, int Class, string? DisplayNumber, int Status);

/// <summary><c>TransferDiscrepancyDto</c>: <c>Kind</c> 1 faltante / 2 sobrante; <c>State</c> Pending, InApproval o Resolved.</summary>
public sealed record DiferenciaDeTrasladoDto
{
    public Guid PublicId { get; init; }
    public int Kind { get; init; }
    public TrasladoDeLaDiferenciaDto Transfer { get; init; } = new(Guid.Empty, null);
    public ReferenciaDeInventarioDto Product { get; init; } = new(Guid.Empty, "", "");
    public decimal QuantityBase { get; init; }
    public decimal PendingBase { get; init; }
    public decimal? Value { get; init; }
    public string State { get; init; } = "";
    public int? Resolution { get; init; }
    public DocumentoQueResuelveDto? ResolvingDocument { get; init; }
    public Guid? ApprovalRequestPublicId { get; init; }
    public ReferenciaDeInventarioDto? DestinationWarehouse { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary><c>TransferDto</c>: el despacho y sus recepciones como documentos, las líneas y las diferencias.</summary>
public sealed record TrasladoDto
{
    public DocumentoDeInventarioDto Dispatch { get; init; } = new();
    public IReadOnlyList<DocumentoDeInventarioDto> Receipts { get; init; } = [];
    public IReadOnlyList<LineaDeTrasladoDto> Lines { get; init; } = [];
    public IReadOnlyList<DiferenciaDeTrasladoDto> Discrepancies { get; init; } = [];
}

/// <summary>Lo recibido de una línea del despacho (§11).</summary>
public sealed record LineaRecibidaRequest(Guid DispatchLinePublicId, decimal ReceivedQuantity, Guid? UnitPublicId = null, Guid? ToLocationPublicId = null,
    decimal? SurplusQuantity = null);

/// <summary>El cuerpo de <c>POST /transfers/{id}/receive</c>.</summary>
public sealed record RecibirTrasladoRequest(DateOnly? OperationDate, Guid? DocumentTypePublicId, IReadOnlyList<LineaRecibidaRequest> Lines, string? Notes);

/// <summary>Una diferencia que dejó la recepción.</summary>
public sealed record DiferenciaCreadaDto(Guid DiscrepancyPublicId, ReferenciaDeInventarioDto Product, decimal QuantityBase);

/// <summary>La respuesta de recibir.</summary>
public sealed record ResultadoDeRecepcionDto
{
    public Guid ReceiptPublicId { get; init; }
    public string? DisplayNumber { get; init; }
    public DateOnly OperationDate { get; init; }
    public int Status { get; init; }
    public IReadOnlyList<DiferenciaCreadaDto> Shortages { get; init; } = [];
    public IReadOnlyList<DiferenciaCreadaDto> Surpluses { get; init; } = [];
}

/// <summary>El cuerpo de <c>POST /transfers/discrepancies/{id}/resolve</c>: <c>Resolution</c> es <c>TransferDiscrepancyResolution</c>.</summary>
public sealed record ResolverDiferenciaRequest(int Resolution, decimal? Quantity, Guid? AdjustmentCausePublicId, DateOnly? OperationDate,
    Guid? ToLocationPublicId, string Reason);

/// <summary>La respuesta de resolver: el documento queda en aprobación.</summary>
public sealed record ResultadoDeResolucionDto
{
    public Guid DiscrepancyPublicId { get; init; }
    public int Resolution { get; init; }
    public Guid DocumentPublicId { get; init; }
    public int DocumentClass { get; init; }
    public int Status { get; init; }
    public Guid? ApprovalRequestPublicId { get; init; }
}

/// <summary>Un destino posible de un traslado (<c>purpose=TransferDestination</c>): sin existencias ni valores.</summary>
public sealed record DestinoDeTrasladoDto(Guid PublicId, string Code, string Name, SucursalDeDestinoDto Branch);

/// <summary>La sucursal de un destino.</summary>
public sealed record SucursalDeDestinoDto(Guid PublicId, string? Code, string Name);
