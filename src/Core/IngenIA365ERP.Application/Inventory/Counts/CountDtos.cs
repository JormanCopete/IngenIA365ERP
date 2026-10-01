using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// El estado derivado de un conteo (contracts/api.md §12, <c>state</c> como texto): borrador (alcance definido, sin foto), abierto
/// (con foto, sin número), cerrado (confirmado y numerado, sin ajuste), ajuste en aprobación, ajustado (ajustes confirmados o sin
/// diferencias), descartado y anulado. (nuevo)
/// </summary>
public static class EstadosDeConteo
{
    public const string Borrador = "Draft";
    public const string Abierto = "Open";
    public const string Cerrado = "Closed";
    public const string AjusteEnAprobacion = "AdjustmentPending";
    public const string Ajustado = "Adjusted";
    public const string Descartado = "Discarded";
    public const string Anulado = "Voided";

    public static readonly IReadOnlyList<string> Todos = [Borrador, Abierto, Cerrado, AjusteEnAprobacion, Ajustado, Descartado, Anulado];
}

/// <summary>La regla de fecha del ajuste (<c>Conteo.FechaDelAjuste</c>): la de la foto o la de la aprobación. (nuevo)</summary>
public static class ReglasDeFechaDelAjuste
{
    public const string Foto = "Foto";
    public const string Aprobacion = "Aprobacion";
}

/// <summary>El cuerpo de <c>POST /counts</c> y <c>PUT /counts/{id}</c> (§12). (nuevo)</summary>
public sealed record PhysicalCountRequest(
    Guid DocumentTypePublicId,
    Guid WarehousePublicId,
    CountKind Kind,
    CountScope Scope,
    IReadOnlyList<Guid>? CategoryPublicIds = null,
    IReadOnlyList<Guid>? LocationPublicIds = null,
    IReadOnlyList<Guid>? ProductPublicIds = null,
    string? AbcClass = null,
    bool Blind = false,
    IReadOnlyList<Guid>? CounterUserPublicIds = null,
    string? Notes = null);

/// <summary>Una fila de la lista (§12, <c>CountSummaryDto</c>). Valores sólo con <c>Inventory.Costs.Read</c>. (nuevo)</summary>
public sealed record CountSummaryDto(
    Guid PublicId,
    string? DisplayNumber,
    string State,
    CountKind Kind,
    CountScope Scope,
    ReferenciaDto Warehouse,
    DateOnly OperationDate,
    DateTime? SnapshotAt,
    int Lines,
    int? LinesWithDifference,
    decimal? DifferenceValue);

/// <summary>El criterio del alcance, como lo ve la pantalla. (nuevo)</summary>
public sealed record CountCriteriaDto(
    IReadOnlyList<ReferenciaDto> Categories,
    IReadOnlyList<ReferenciaDto> Locations,
    IReadOnlyList<ReferenciaDto> Products,
    string? AbcClass);

/// <summary>
/// Una línea del conteo. En un conteo ciego, <see cref="Theoretical"/>, <see cref="Difference"/> y <see cref="DifferenceValue"/>
/// salen nulos para quien sólo captura; los valores exigen <c>Inventory.Costs.Read</c>. (nuevo)
/// </summary>
public sealed record CountLineDto(
    ReferenciaDto Product,
    UnidadDto BaseUnit,
    ReferenciaDto Location,
    string? Lot,
    decimal? Theoretical,
    decimal? MovementsAfterSnapshot,
    decimal? CountedRound1,
    decimal? CountedRound2,
    decimal Counted,
    decimal? Difference,
    decimal? UnitCost,
    decimal? DifferenceValue,
    bool NeedsRecount,
    bool AddedDuringCapture);

/// <summary>Un ajuste generado por el conteo. (nuevo)</summary>
public sealed record CountAdjustmentRefDto(Guid DocumentPublicId, DocumentClass Class, string? DisplayNumber, DocumentStatus Status,
    DateOnly OperationDate, Guid? ApprovalRequestPublicId);

/// <summary><c>PhysicalCountDto</c> (§12): el conteo con su criterio, su foto, sus contadores, sus líneas y sus ajustes. (nuevo)</summary>
public sealed record PhysicalCountDto(
    Guid PublicId,
    string? DisplayNumber,
    string State,
    ReferenciaDto DocumentType,
    CountKind Kind,
    CountScope Scope,
    ReferenciaDto Warehouse,
    CountCriteriaDto Criteria,
    bool Blind,
    bool BlocksMovements,
    DateOnly OperationDate,
    DateTime? SnapshotAt,
    DateOnly? SnapshotDate,
    byte Round,
    UsuarioDto? OpenedBy,
    IReadOnlyList<UsuarioDto> Counters,
    IReadOnlyList<CountLineDto> Lines,
    IReadOnlyList<CountAdjustmentRefDto> Adjustments,
    string? Notes,
    bool ShowsTheoretical,
    byte[]? RowVersion);

/// <summary>La respuesta de <c>POST /{id}/open</c>. (nuevo)</summary>
public sealed record OpenPhysicalCountResultDto(Guid PublicId, DateTime SnapshotAt, DateOnly SnapshotDate, int Lines, bool BlocksMovements);

/// <summary>Una lectura del lector o digitada (§12): por código de barras o por producto; cantidad 1 por defecto en la unidad leída. (nuevo)</summary>
public sealed record CountReadRequest(
    string? Barcode = null,
    Guid? ProductPublicId = null,
    Guid? UnitPublicId = null,
    decimal? Quantity = null,
    Guid? LocationPublicId = null,
    string? LotCode = null,
    DateTime? ReadAt = null);

/// <summary>Una lectura rechazada (se acepta o rechaza cada una sola). (nuevo)</summary>
public sealed record RejectedReadDto(int Index, string Code, string Message);

/// <summary>Una línea tocada por la tanda: <see cref="Counted"/> nulo en un conteo ciego para quien sólo captura. (nuevo)</summary>
public sealed record CapturedLineDto(Guid ProductPublicId, string ProductCode, Guid LocationPublicId, decimal? Counted, bool AddedDuringCapture);

/// <summary>La respuesta de <c>POST /{id}/captures</c>. (nuevo)</summary>
public sealed record CaptureResultDto(int Accepted, IReadOnlyList<RejectedReadDto> Rejected, IReadOnlyList<CapturedLineDto> Lines);

/// <summary>Una captura (§12, <c>GET /{id}/captures</c>): la cantidad va en unidad base. (nuevo)</summary>
public sealed record CountCaptureDto(
    Guid CapturePublicId,
    UsuarioDto Counter,
    byte Round,
    ReferenciaDto Product,
    UnidadDto Unit,
    decimal Quantity,
    decimal QuantityBase,
    int Reads,
    bool IsCorrection,
    ReferenciaDto Location,
    string? Lot,
    DateTime ReadAt,
    DateTime RegisteredAt);

/// <summary>La respuesta de <c>POST /{id}/close</c>. (nuevo)</summary>
public sealed record ClosePhysicalCountResultDto(Guid PublicId, string State, long? Number, string? DisplayNumber, int Lines, int WithDifference,
    decimal? DifferenceValue);

/// <summary>Una línea de la vista previa del ajuste. (nuevo)</summary>
public sealed record CountAdjustmentPreviewLineDto(ReferenciaDto Product, ReferenciaDto Location, string? Lot, decimal Theoretical, decimal Counted,
    decimal Difference, decimal? UnitCost, decimal? Value);

/// <summary>La vista previa del ajuste (<c>GET /{id}/adjustment</c>). (nuevo)</summary>
public sealed record CountAdjustmentPreviewDto(DateOnly AdjustmentDate, string DateRule, IReadOnlyList<CountAdjustmentPreviewLineDto> Lines,
    decimal? PositiveValue, decimal? NegativeValue, IReadOnlyList<CountAdjustmentRefDto> Existing);

/// <summary>Un documento generado por <c>POST /{id}/adjustment</c>. (nuevo)</summary>
public sealed record GeneratedAdjustmentDto(Guid DocumentPublicId, DocumentClass Class, int Lines, decimal? Value, DocumentStatus Status,
    Guid? ApprovalRequestPublicId);

/// <summary>La respuesta de <c>POST /{id}/adjustment</c>: vacía si no había diferencias (el conteo queda <c>Adjusted</c>). (nuevo)</summary>
public sealed record CountAdjustmentResultDto(IReadOnlyList<GeneratedAdjustmentDto> Documents);
