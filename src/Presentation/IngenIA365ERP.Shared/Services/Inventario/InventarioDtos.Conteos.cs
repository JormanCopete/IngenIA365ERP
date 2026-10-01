namespace IngenIA365ERP.Shared.Services.Inventario;

// Espejos de los conteos físicos (feature 012, US11, T403; contracts/api.md §12). Los enums viajan como número (Kind: CountKind,
// Scope: CountScope, Class, Status) y el estado derivado como texto (State); la pantalla los traduce con TextosDeInventario.

/// <summary>Los filtros de la lista de conteos: <c>State</c> es uno de <see cref="TextosDeInventario.EstadosDeConteo"/>.</summary>
public sealed record FiltroDeConteos
{
    public string? State { get; init; }
    public Guid? WarehousePublicId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>El cuerpo de <c>POST /counts</c> y <c>PUT /counts/{id}</c>: <c>Kind</c> 1 total / 2 cíclico; <c>Scope</c> 0 todo, 1 categoría, 2 ubicación, 3 selección.</summary>
public sealed record DefinicionDeConteoRequest(
    Guid DocumentTypePublicId,
    Guid WarehousePublicId,
    int Kind,
    int Scope,
    IReadOnlyList<Guid>? CategoryPublicIds,
    IReadOnlyList<Guid>? LocationPublicIds,
    IReadOnlyList<Guid>? ProductPublicIds,
    string? AbcClass,
    bool Blind,
    IReadOnlyList<Guid>? CounterUserPublicIds,
    string? Notes);

/// <summary><c>CountSummaryDto</c>: <see cref="DifferenceValue"/> sólo con <c>Inventory.Costs.Read</c>.</summary>
public sealed record ResumenDeConteoDto
{
    public Guid PublicId { get; init; }
    public string? DisplayNumber { get; init; }
    public string State { get; init; } = "";
    public int Kind { get; init; }
    public int Scope { get; init; }
    public ReferenciaDeInventarioDto Warehouse { get; init; } = new(Guid.Empty, "", "");
    public DateOnly OperationDate { get; init; }
    public DateTime? SnapshotAt { get; init; }
    public int Lines { get; init; }
    public int? LinesWithDifference { get; init; }
    public decimal? DifferenceValue { get; init; }
}

/// <summary>El criterio del alcance.</summary>
public sealed record CriterioDeConteoDto
{
    public IReadOnlyList<ReferenciaDeInventarioDto> Categories { get; init; } = [];
    public IReadOnlyList<ReferenciaDeInventarioDto> Locations { get; init; } = [];
    public IReadOnlyList<ReferenciaDeInventarioDto> Products { get; init; } = [];
    public string? AbcClass { get; init; }
}

/// <summary>Una línea del conteo: en un conteo ciego, teórico y diferencia llegan nulos a quien sólo captura.</summary>
public sealed record LineaDeConteoDto
{
    public ReferenciaDeInventarioDto Product { get; init; } = new(Guid.Empty, "", "");
    public UnidadDeLineaDto BaseUnit { get; init; } = new(Guid.Empty, "");
    public ReferenciaDeInventarioDto Location { get; init; } = new(Guid.Empty, "", "");
    public string? Lot { get; init; }
    public decimal? Theoretical { get; init; }
    public decimal? MovementsAfterSnapshot { get; init; }
    public decimal? CountedRound1 { get; init; }
    public decimal? CountedRound2 { get; init; }
    public decimal Counted { get; init; }
    public decimal? Difference { get; init; }
    public decimal? UnitCost { get; init; }
    public decimal? DifferenceValue { get; init; }
    public bool NeedsRecount { get; init; }
    public bool AddedDuringCapture { get; init; }
}

/// <summary>Un ajuste del conteo (<c>Class</c> y <c>Status</c> como número).</summary>
public sealed record AjusteDelConteoDto(Guid DocumentPublicId, int Class, string? DisplayNumber, int Status, DateOnly OperationDate, Guid? ApprovalRequestPublicId);

/// <summary><c>PhysicalCountDto</c> (§12).</summary>
public sealed record ConteoDto
{
    public Guid PublicId { get; init; }
    public string? DisplayNumber { get; init; }
    public string State { get; init; } = "";
    public ReferenciaDeInventarioDto DocumentType { get; init; } = new(Guid.Empty, "", "");
    public int Kind { get; init; }
    public int Scope { get; init; }
    public ReferenciaDeInventarioDto Warehouse { get; init; } = new(Guid.Empty, "", "");
    public CriterioDeConteoDto Criteria { get; init; } = new();
    public bool Blind { get; init; }
    public bool BlocksMovements { get; init; }
    public DateOnly OperationDate { get; init; }
    public DateTime? SnapshotAt { get; init; }
    public DateOnly? SnapshotDate { get; init; }
    public int Round { get; init; }
    public UsuarioDeInventarioDto? OpenedBy { get; init; }
    public IReadOnlyList<UsuarioDeInventarioDto> Counters { get; init; } = [];
    public IReadOnlyList<LineaDeConteoDto> Lines { get; init; } = [];
    public IReadOnlyList<AjusteDelConteoDto> Adjustments { get; init; } = [];
    public string? Notes { get; init; }
    public bool ShowsTheoretical { get; init; }
    public byte[]? RowVersion { get; init; }
}

/// <summary>La respuesta de abrir.</summary>
public sealed record ResultadoDeAperturaDeConteoDto(Guid PublicId, DateTime SnapshotAt, DateOnly SnapshotDate, int Lines, bool BlocksMovements);

/// <summary>Una lectura: por código de barras o por producto; cantidad 1 por defecto en la unidad leída (negativa corrige).</summary>
public sealed record LecturaDeConteoRequest(string? Barcode, Guid? ProductPublicId, Guid? UnitPublicId, decimal? Quantity, Guid? LocationPublicId,
    string? LotCode, DateTime? ReadAt);

/// <summary>El cuerpo de <c>POST /counts/{id}/captures</c>.</summary>
public sealed record CapturaDeConteoRequest(int Round, IReadOnlyList<LecturaDeConteoRequest> Reads);

/// <summary>Una lectura rechazada: su posición en la tanda, el código y el mensaje.</summary>
public sealed record LecturaRechazadaDto(int Index, string Code, string Message);

/// <summary>Una línea tocada por la tanda.</summary>
public sealed record LineaCapturadaDto(Guid ProductPublicId, string ProductCode, Guid LocationPublicId, decimal? Counted, bool AddedDuringCapture);

/// <summary>La respuesta de capturar.</summary>
public sealed record ResultadoDeCapturaDto
{
    public int Accepted { get; init; }
    public IReadOnlyList<LecturaRechazadaDto> Rejected { get; init; } = [];
    public IReadOnlyList<LineaCapturadaDto> Lines { get; init; } = [];
}

/// <summary>Una captura registrada (cantidad en unidad base).</summary>
public sealed record CapturaRegistradaDto
{
    public Guid CapturePublicId { get; init; }
    public UsuarioDeInventarioDto Counter { get; init; } = new(null, "");
    public int Round { get; init; }
    public ReferenciaDeInventarioDto Product { get; init; } = new(Guid.Empty, "", "");
    public UnidadDeLineaDto Unit { get; init; } = new(Guid.Empty, "");
    public decimal Quantity { get; init; }
    public decimal QuantityBase { get; init; }
    public int Reads { get; init; }
    public bool IsCorrection { get; init; }
    public ReferenciaDeInventarioDto Location { get; init; } = new(Guid.Empty, "", "");
    public DateTime ReadAt { get; init; }
    public DateTime RegisteredAt { get; init; }
}

/// <summary>La respuesta de cerrar.</summary>
public sealed record ResultadoDeCierreDeConteoDto(Guid PublicId, string State, long? Number, string? DisplayNumber, int Lines, int WithDifference, decimal? DifferenceValue);

/// <summary>Una línea de la vista previa del ajuste.</summary>
public sealed record LineaDelAjustePrevistoDto(ReferenciaDeInventarioDto Product, ReferenciaDeInventarioDto Location, string? Lot, decimal Theoretical,
    decimal Counted, decimal Difference, decimal? UnitCost, decimal? Value);

/// <summary>La vista previa del ajuste: <c>DateRule</c> es <c>Foto</c> o <c>Aprobacion</c>.</summary>
public sealed record AjustePrevistoDto(DateOnly AdjustmentDate, string DateRule, IReadOnlyList<LineaDelAjustePrevistoDto> Lines, decimal? PositiveValue,
    decimal? NegativeValue, IReadOnlyList<AjusteDelConteoDto> Existing);

/// <summary>Un documento generado por el ajuste (en aprobación).</summary>
public sealed record AjusteGeneradoDto(Guid DocumentPublicId, int Class, int Lines, decimal? Value, int Status, Guid? ApprovalRequestPublicId);

/// <summary>La respuesta de generar el ajuste: vacía si no había diferencias.</summary>
public sealed record ResultadoDeAjusteDeConteoDto(IReadOnlyList<AjusteGeneradoDto> Documents);

/// <summary>Una persona de la cooperativa que puede declararse como contador.</summary>
public sealed record PersonaDeLaCooperativaDto(Guid PublicId, string Username, bool IsActive);

/// <summary>El cuerpo de <c>POST /counts/{id}/adjustment</c>.</summary>
public sealed record AjusteDeConteoRequest(string? Notes);
