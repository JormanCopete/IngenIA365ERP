using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Periods;

/// <summary>
/// <c>GET /api/inventory/periods/{year}/{month}/close</c> (feature 012, T289; contracts/api.md §13.4): la vista previa del
/// cierre —bloqueos, avisos y remisiones sin facturar— sin tocar nada. (nuevo)
/// </summary>
public sealed record GetPeriodCloseCheckQuery(int Year, int Month) : IRequest<Result<PeriodCloseCheckDto>>;

public sealed class GetPeriodCloseCheckQueryValidator : AbstractValidator<GetPeriodCloseCheckQuery>
{
    public GetPeriodCloseCheckQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 9999);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class GetPeriodCloseCheckQueryHandler(RevisionDeCierre revision) : IRequestHandler<GetPeriodCloseCheckQuery, Result<PeriodCloseCheckDto>>
{
    public Task<Result<PeriodCloseCheckDto>> Handle(GetPeriodCloseCheckQuery request, CancellationToken ct) =>
        revision.RevisarAsync(request.Year, request.Month, ct);
}

/// <summary>
/// Cierra un mes de inventario de toda la cooperativa (feature 012, T289; FR-047, SC-017; contracts/api.md §13.4; data-model
/// §6.1–§6.3), <c>Inventory.Periods.Close</c>. En orden:
/// <list type="number">
/// <item>toma <c>INV_Setup</c> en <b>exclusivo</b> (<see cref="ModoDeBloqueoDelSetup.Exclusivo"/>): espera las confirmaciones en
/// vuelo y detiene las nuevas mientras dura;</item>
/// <item>revisa con <see cref="RevisionDeCierre"/>: sólo el siguiente al último cerrado (<c>Inventory.Period.NotNext</c>), sólo
/// un mes terminado (<c>.NotEnded</c>), sin conteos abiertos con foto (<c>.OpenCounts</c>, US11); con avisos, el primer
/// intento responde <c>.WarningsNotAcknowledged</c> y con <see cref="CloseInventoryPeriodCommand.AcknowledgeWarnings"/> cierra
/// y los guarda en <c>CloseWarningsJson</c>; las remisiones sin facturar (I6) exigen aceptarlas con permiso y motivo;</item>
/// <item>fija el valorizado del fin del mes (<see cref="ValorizadoALaFecha"/>) en <c>INV_PeriodClosingBalances</c> versión
/// <c>CloseVersion</c> (1 en el primer cierre, +1 en cada recierre) con el grupo contable a esa fecha
/// (<see cref="GrupoContableALaFecha"/>), sin filas en cero;</item>
/// <item>pone <c>LastClosedDate</c> en el último día del mes y emite <c>PeriodoInventarioCerrado</c> (informativo,
/// <c>Close:{closingVersion}</c>) en el mismo guardado.</item>
/// </list>
/// El lote contable del disparador <c>CierreDePeriodo</c> es de US7 (I2, <c>OrderIntegrationBatchCommand</c>): en I1 ningún
/// tipo lo usa y <c>batchPublicId</c> sale nulo. (nuevo)
/// </summary>
public sealed record CloseInventoryPeriodCommand(int Year, int Month, bool AcknowledgeWarnings = false, bool AcceptUnbilledShipments = false, string? Reason = null)
    : IRequest<Result<ClosePeriodResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseInventoryPeriodCommandValidator : AbstractValidator<CloseInventoryPeriodCommand>
{
    public CloseInventoryPeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 9999);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ReopenInventoryPeriodCommand>.LargoMaximo);
    }
}

public sealed class CloseInventoryPeriodCommandHandler(
    IApplicationDbContext db,
    RevisionDeCierre revision,
    ValorizadoALaFecha valorizado,
    ICerrojoDeInventario cerrojo,
    EmisorDeMensajes emisor,
    IActorActual actorActual,
    IPermissionChecker permisos,
    IDateTimeService reloj)
    : IRequestHandler<CloseInventoryPeriodCommand, Result<ClosePeriodResultDto>>
{
    public const string PermisoDeRemisiones = "Inventory.Periods.AcceptUnbilledShipments";
    public const string PermisoDeCostos = "Inventory.Costs.Read";

    public Task<Result<ClosePeriodResultDto>> Handle(CloseInventoryPeriodCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => CerrarAsync(request, ct), ct);

    private async Task<Result<ClosePeriodResultDto>> CerrarAsync(CloseInventoryPeriodCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        await cerrojo.BloquearAsync(new PedidoDeCerrojo { Setup = ModoDeBloqueoDelSetup.Exclusivo }, ct);
        var setup = await db.InventorySetups.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (setup is null) return Falla(InventoryErrors.PeriodNotStarted());

        var revisada = await revision.RevisarAsync(request.Year, request.Month, ct);
        if (revisada.IsFailure) return Falla(revisada.Error);
        var check = revisada.Value;
        if (check.Blockers.NotNext is { } siguiente) return Falla(InventoryErrors.PeriodNotNext(request.Year, request.Month, siguiente));
        if (check.Blockers.NotEnded is { } ultimoDia) return Falla(InventoryErrors.PeriodNotEnded(request.Year, request.Month, ultimoDia));
        if (check.Blockers.OpenCounts.Count > 0) return Falla(InventoryErrors.PeriodOpenCounts(request.Year, request.Month, check.Blockers.OpenCounts));
        if (check.Warnings.Any && !request.AcknowledgeWarnings)
            return Falla(InventoryErrors.PeriodWarningsNotAcknowledged(request.Year, request.Month, check.Warnings));
        if (check.UnbilledShipments.Count > 0)
        {
            if (!request.AcceptUnbilledShipments || string.IsNullOrWhiteSpace(request.Reason))
                return Falla(InventoryErrors.PeriodUnbilledShipmentsNotAccepted(request.Year, request.Month, check.UnbilledShipments));
            if (!await permisos.HasPermissionAsync(PermisoDeRemisiones, ct)) return Falla(InventoryErrors.PeriodAcceptUnbilledNotAllowed(PermisoDeRemisiones));
        }

        var inicio = new DateOnly(request.Year, request.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        var ahora = reloj.UtcNow;

        // El valorizado del fin del mes (antes de tocar el período: parte del cierre anterior guardado), con el grupo a esa fecha.
        var filas = await valorizado.CalcularAsync(fin, null, ct);
        var grupos = await GrupoContableALaFecha.DeAsync(db, filas.Select(f => f.ProductId).Distinct().ToList(), fin, ct);

        var periodo = await db.InventoryPeriods.FirstOrDefaultAsync(p => p.Year == request.Year && p.Month == request.Month, ct);
        if (periodo is null)
        {
            periodo = new InventoryPeriod { Year = (short)request.Year, Month = (byte)request.Month };
            db.InventoryPeriods.Add(periodo);
        }
        var version = periodo.CloseVersion + 1;
        periodo.Status = InventoryPeriodStatus.Closed;
        periodo.CloseVersion = version;
        periodo.ClosedAt = ahora;
        periodo.ClosedByUserId = usuario;
        periodo.CloseWarningsJson = check.Warnings.Any ? JsonSerializer.Serialize(check.Warnings, Json) : null;
        if (check.UnbilledShipments.Count > 0)
        {
            periodo.UnbilledShipmentsJson = JsonSerializer.Serialize(check.UnbilledShipments, Json);
            periodo.UnbilledShipmentsAcceptedByUserId = usuario;
            periodo.UnbilledShipmentsAcceptedReason = request.Reason!.Trim();
        }

        // Un producto sin grupo (no debería tener kardex: los inventariables lo exigen) no se puede fijar por grupo y se omite.
        var fijadas = new List<PeriodClosingBalance>(filas.Count);
        foreach (var fila in filas)
        {
            if (grupos.GetValueOrDefault(fila.ProductId) is not int grupo) continue;
            var saldo = new PeriodClosingBalance
            {
                Period = periodo, PeriodId = periodo.Id, Version = version, ProductId = fila.ProductId, WarehouseId = fila.WarehouseId,
                AccountingGroupId = grupo, Quantity = fila.Quantity, Value = fila.Value,
            };
            db.PeriodClosingBalances.Add(saldo);
            fijadas.Add(saldo);
        }
        setup.LastClosedDate = fin;

        // El mensaje informativo, en el mismo guardado.
        var dimensiones = await DimensionesAsync(fijadas, ct);
        var contenido = new PeriodoInventarioCerradoV1
        {
            Year = request.Year,
            Month = request.Month,
            ClosingVersion = version,
            Valuation = fijadas
                .GroupBy(f => (f.AccountingGroupId, f.WarehouseId))
                .Select(g => new PeriodValuationLineV1
                {
                    AccountingGroupCode = dimensiones.Grupos[g.Key.AccountingGroupId].Code,
                    WarehouseCode = dimensiones.Bodegas[g.Key.WarehouseId].Code,
                    WarehouseBehavior = dimensiones.Bodegas[g.Key.WarehouseId].Behavior,
                    BranchPublicId = dimensiones.Bodegas[g.Key.WarehouseId].Sucursal,
                    QuantityBase = g.Sum(f => f.Quantity),
                    Value = g.Sum(f => f.Value),
                })
                .OrderBy(l => l.AccountingGroupCode, StringComparer.Ordinal).ThenBy(l => l.WarehouseCode, StringComparer.Ordinal)
                .ToList(),
            AcknowledgedPending = new AcknowledgedPendingV1
            {
                Pending = check.Warnings.Messages.Pending, InBatch = check.Warnings.Messages.InBatch, Rejected = check.Warnings.Messages.Rejected,
            },
        };
        var emitidos = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, periodo.PublicId, null, null, $"{request.Year:0000}-{request.Month:00}", fin,
                await OperacionesDeInventario.SucursalPrincipalAsync(db, ct)),
            ClavesDeEvento.Cierre(version), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Online)), ct);

        await db.SaveChangesAsync(ct);

        var conCostos = await permisos.HasPermissionAsync(PermisoDeCostos, ct);
        var valorizadoDto = contenido.Valuation.Select(l =>
            {
                var grupo = dimensiones.Grupos.Values.First(g => g.Code == l.AccountingGroupCode);
                var bodega = dimensiones.Bodegas.Values.First(b => b.Code == l.WarehouseCode);
                return new PeriodValuationDto(new CodigoYNombreDto(grupo.Code, grupo.Name), new BodegaDelValorizadoDto(bodega.PublicId, bodega.Code),
                    l.QuantityBase, conCostos ? l.Value : null);
            })
            .ToList();
        return Result.Success(new ClosePeriodResultDto(request.Year, request.Month, ahora, version, valorizadoDto,
            conCostos ? contenido.Valuation.Sum(l => l.Value) : null, emitidos[0].PublicId, null));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<(Dictionary<int, (string Code, string Name)> Grupos, Dictionary<int, (Guid PublicId, string Code, WarehouseBehavior Behavior, Guid Sucursal)> Bodegas)>
        DimensionesAsync(IReadOnlyList<PeriodClosingBalance> filas, CancellationToken ct)
    {
        var grupoIds = filas.Select(f => f.AccountingGroupId).Distinct().ToList();
        var bodegaIds = filas.Select(f => f.WarehouseId).Distinct().ToList();
        var grupos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(g => grupoIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => (g.Code, g.Name), ct);
        var bodegas = await (from w in db.Warehouses.AsNoTracking().IgnoreQueryFilters()
                             join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on w.BranchId equals b.Id
                             where bodegaIds.Contains(w.Id)
                             select new { w.Id, w.PublicId, w.Code, w.Behavior, Sucursal = b.PublicId })
            .ToDictionaryAsync(w => w.Id, w => (w.PublicId, w.Code, w.Behavior, w.Sucursal), ct);
        return (grupos, bodegas);
    }

    private static Result<ClosePeriodResultDto> Falla(Error error) => Result.Failure<ClosePeriodResultDto>(error);
}
