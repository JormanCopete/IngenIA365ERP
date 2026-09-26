namespace IngenIA365ERP.Shared.Services.Inventario;

// DTO espejo de períodos y del cambio de grupo contable (feature 012, T294; contracts/api.md §3.6.4, §13.4). Los enums llegan
// como número (Status: 1 abierto, 2 cerrado); los valores llegan nulos sin Inventory.Costs.Read.

/// <summary><c>GET /api/inventory/periods</c>.</summary>
public sealed record PeriodosDeInventarioDto(DateOnly? StartDate, DateOnly? LastClosedDate, IReadOnlyList<PeriodoDeInventarioDto> Periods);

/// <summary>Un mes con su estado. <see cref="Status"/>: 1 abierto, 2 cerrado.</summary>
public sealed record PeriodoDeInventarioDto(
    int Year,
    int Month,
    int Status,
    DateTime? ClosedAt,
    UsuarioDeInventarioDto? ClosedBy,
    DateTime? ReopenedAt,
    UsuarioDeInventarioDto? ReopenedBy,
    string? ReopenReason,
    int? ClosingVersion)
{
    public bool Cerrado => Status == 2;
}

/// <summary>Un mes como lo nombran los bloqueos (<c>{ year, month }</c>).</summary>
public sealed record MesDto(int Year, int Month);

/// <summary>Un conteo abierto con foto (US11).</summary>
public sealed record ConteoAbiertoDto(Guid CountPublicId, string? DisplayNumber, string Warehouse, DateOnly SnapshotDate);

public sealed record BloqueosDelCierreDto(IReadOnlyList<ConteoAbiertoDto> OpenCounts, MesDto? NotNext, DateOnly? NotEnded);

public sealed record TrasladoSinResolverDto(Guid DispatchPublicId, string? DisplayNumber, decimal PendingBase);

public sealed record MensajesPendientesDto(int Pending, int InBatch, int Rejected);

public sealed record AvisosDelCierreDto(int Drafts, int PendingApproval, IReadOnlyList<TrasladoSinResolverDto> UnresolvedTransfers, MensajesPendientesDto Messages)
{
    public bool Hay => Drafts > 0 || PendingApproval > 0 || UnresolvedTransfers.Count > 0 || Messages.Pending + Messages.InBatch + Messages.Rejected > 0;
}

public sealed record RemisionSinFacturarDto(Guid DocumentPublicId, string? DisplayNumber, string Customer, decimal Value, string ValueSource);

/// <summary><c>GET /periods/{year}/{month}/close</c>: la vista previa.</summary>
public sealed record RevisionDelCierreDto(
    int Year,
    int Month,
    bool CanClose,
    BloqueosDelCierreDto Blockers,
    AvisosDelCierreDto Warnings,
    IReadOnlyList<RemisionSinFacturarDto> UnbilledShipments,
    decimal UnbilledTotal);

/// <summary>El cuerpo de cerrar.</summary>
public sealed record CerrarPeriodoRequest(bool AcknowledgeWarnings, bool AcceptUnbilledShipments = false, string? Reason = null);

public sealed record CodigoYNombreDeInventarioDto(string Code, string Name);

public sealed record BodegaDelValorizadoDto(Guid PublicId, string Code);

public sealed record LineaDelValorizadoDto(CodigoYNombreDeInventarioDto AccountingGroup, BodegaDelValorizadoDto Warehouse, decimal Quantity, decimal? Value);

/// <summary>Lo que devuelve cerrar: el valorizado fijado por grupo y bodega.</summary>
public sealed record ResultadoDelCierreDto(
    int Year, int Month, DateTime ClosedAt, int ClosingVersion, IReadOnlyList<LineaDelValorizadoDto> Valuation, decimal? Total,
    Guid MessagePublicId, Guid? BatchPublicId);

/// <summary>El cuerpo de reabrir.</summary>
public sealed record ReabrirPeriodoRequest(string Reason);

public sealed record ResultadoDeLaReaperturaDto(int Year, int Month, DateTime ReopenedAt, int SupersededClosingVersion, Guid MessagePublicId);

// ----------------------------------------------------------------------------------------- grupo contable --

public sealed record GrupoDelCambioDto(Guid PublicId, string Code, string Name);

/// <summary>Una fila del historial del grupo contable de un producto.</summary>
public sealed record CambioDeGrupoDto(
    Guid ChangePublicId, GrupoDelCambioDto From, GrupoDelCambioDto To, DateOnly EffectiveDate, string Reason, string? ChangedBy,
    DateTime ChangedAt, decimal Quantity, decimal? Value, Guid? MessagePublicId);

/// <summary>El cuerpo del cambio de grupo contable.</summary>
public sealed record CambiarGrupoContableRequest(Guid AccountingGroupPublicId, DateOnly? EffectiveDate, string Reason);

public sealed record BodegaDelCambioDto(Guid WarehousePublicId, string WarehouseCode, decimal Quantity, decimal? Value);

/// <summary>Lo que devuelve el cambio de grupo contable.</summary>
public sealed record ResultadoDelCambioDeGrupoDto(
    Guid ChangePublicId, GrupoDelCambioDto From, GrupoDelCambioDto To, DateOnly EffectiveDate, IReadOnlyList<BodegaDelCambioDto> ByWarehouse,
    Guid? MessagePublicId);
