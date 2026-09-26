using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// Abre un conteo físico (feature 012, US11, T392; FR-040, US11-1; contracts/api.md §12, <c>POST /counts/{id}/open</c> con
/// <c>{ rowVersion }</c>, permiso <c>Inventory.Counts.Open</c>): congela la foto teórica del alcance en <c>INV_CountSnapshotLines</c>
/// y sella si bloquea los movimientos. El conteo sigue en <c>Draft</c> <b>sin número</b> —se numera al cerrar—. (nuevo)
/// </summary>
public sealed record OpenPhysicalCountCommand(Guid CountPublicId) : IRequest<Result<OpenPhysicalCountResultDto>>, IOperacionIdempotente
{
    public byte[]? RowVersion { get; init; }

    public Guid OperationKey { get; init; }
}

public sealed class OpenPhysicalCountCommandValidator : AbstractValidator<OpenPhysicalCountCommand>
{
    public OpenPhysicalCountCommandValidator() => RuleFor(x => x.CountPublicId).NotEmpty();
}

/// <summary>
/// En orden, dentro de una <see cref="TransaccionExplicita"/>:
/// <list type="number">
/// <item>el conteo existe en el alcance (404), está en borrador y sin foto (<c>Inventory.Count.AlreadyOpen</c>), sin cambios desde
/// que se leyó; la clase ABC llega en I6 (<c>.ScopeNotAvailable</c>); la bodega sigue activa; la fecha de hoy no cae en un período
/// cerrado ni antes del inicio;</item>
/// <item>toma <b>en exclusivo</b> la fila de la bodega por <see cref="ICerrojoDeInventario"/>: espera a que terminen las
/// confirmaciones en vuelo (que la tienen compartida) y ninguna entra mientras copia;</item>
/// <item>copia de <c>INV_StockDetails</c> las existencias distintas de cero del alcance (todo, categorías con sus descendientes,
/// ubicaciones o selección), con el costo promedio de su ámbito (<c>SnapshotUnitCost</c>); sin ninguna,
/// <c>Inventory.Count.EmptyScope</c>;</item>
/// <item>un producto que ya está en otro conteo abierto de la bodega: <c>Inventory.Count.Overlaps</c>;</item>
/// <item>sella <c>CountSnapshotAt</c>, el mayor Id del kardex (<c>CountSnapshotKardexEntryId</c>), la ronda 1, la fecha de la foto,
/// quién abrió y <c>Conteo.BloquearMovimientos</c> de la bodega a esa fecha.</item>
/// </list>
/// </summary>
public sealed class OpenPhysicalCountCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IMaestrosDelDocumento maestros,
    ICerrojoDeInventario cerrojo,
    ILectorDeParametros parametros,
    VistaDeConteos conteos)
    : IRequestHandler<OpenPhysicalCountCommand, Result<OpenPhysicalCountResultDto>>
{
    public Task<Result<OpenPhysicalCountResultDto>> Handle(OpenPhysicalCountCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => AbrirAsync(request, ct), ct);

    private async Task<Result<OpenPhysicalCountResultDto>> AbrirAsync(OpenPhysicalCountCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) El conteo y sus reglas.
        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: true, ct);
        if (conteo is null) return Falla(ErroresDeConteos.NotFound());
        if (conteo.Status != DocumentStatus.Draft) return Falla(InventoryErrors.NotDraft(conteo.Status));
        if (conteo.CountSnapshotAt is { } foto) return Falla(ErroresDeConteos.AlreadyOpen(foto));
        if (request.RowVersion is { Length: > 0 } leida && conteo.RowVersion is { Length: > 0 } actual && !leida.AsSpan().SequenceEqual(actual))
            return Falla(Error.StaleRowVersion);
        if (conteo.CountScope == CountScope.AbcClass) return Falla(ErroresDeConteos.ScopeNotAvailable(CountScope.AbcClass));
        if (conteo.WarehouseId is not int bodegaId) return Falla(InventoryErrors.FieldRequired(ReglasDelDocumento.CampoBodega));

        var bodega = (await maestros.BodegasPorIdAsync([bodegaId], ct)).First();
        if (bodega.Inactiva) return Falla(InventoryErrors.WarehouseInactive(bodega.PublicId, bodega.Code));
        if (!bodega.Activa) return Falla(InventoryErrors.WarehouseNotActive(bodega.PublicId, bodega.Code));
        var hoy = reloj.HoyLocal;
        var corte = await maestros.CorteAsync(ct);
        if (corte.StartDate is { } inicio && hoy < inicio) return Falla(InventoryErrors.DateBeforeCutoff(inicio));
        if (corte.LastClosedDate is { } cerrado && hoy <= cerrado) return Falla(InventoryErrors.PeriodClosed(hoy.Year, hoy.Month, cerrado));

        var leidos = await conteos.ParametrosAsync(bodegaId, hoy, ct);
        if (leidos.IsFailure) return Falla(leidos.Error);

        // (2) La bodega en exclusivo: lo que se confirma en ella termina antes de la foto, y nada entra mientras se copia.
        await cerrojo.BloquearAsync(new PedidoDeCerrojo { Bodegas = [bodegaId], BodegasEnExclusivo = true }, ct);

        // (3) La foto del alcance.
        var criterio = CriterioDelConteo.De(conteo.CountScopeJson);
        var existencias = db.StockDetails.AsNoTracking().Where(s => s.WarehouseId == bodegaId && s.Quantity != 0m);
        switch (conteo.CountScope)
        {
            case CountScope.Location:
                existencias = existencias.Where(s => criterio.Ubicaciones.Contains(s.LocationId));
                break;
            case CountScope.Selection:
                existencias = existencias.Where(s => criterio.Productos.Contains(s.ProductId));
                break;
            case CountScope.Category:
                var categorias = await conteos.CategoriasConDescendientesAsync(criterio.Categorias, ct);
                existencias = existencias.Where(s => db.Products.Any(p => p.Id == s.ProductId && categorias.Contains(p.CategoryId)));
                break;
        }
        var filas = await existencias.OrderBy(s => s.ProductId).ThenBy(s => s.LocationId).ThenBy(s => s.LotId).ToListAsync(ct);
        if (filas.Count == 0) return Falla(ErroresDeConteos.EmptyScope());

        // (4) Un producto, un conteo abierto por bodega.
        var productos = filas.Select(f => f.ProductId).Distinct().ToList();
        var otros = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.PhysicalCount && d.Status == DocumentStatus.Draft && d.CountSnapshotAt != null
                && d.WarehouseId == bodegaId && d.Id != conteo.Id)
            .OrderBy(d => d.Id).ToListAsync(ct);
        foreach (var otro in otros)
        {
            var cruzados = await conteos.ProductosEnElAlcanceAsync(otro, productos, ct);
            if (cruzados.Count == 0) continue;
            var codigos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => cruzados.Contains(p.Id))
                .OrderBy(p => p.Code).Select(p => p.Code).ToListAsync(ct);
            return Falla(ErroresDeConteos.Overlaps(otro.PublicId, codigos));
        }

        // (5) El costo del ámbito de cada producto (informativo) y el sello.
        var ambito = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoAmbito, hoy, ct: ct);
        if (ambito.IsFailure) return Falla(ambito.Error);
        var ambitoId = ambito.Value.Texto == "Bodega" ? bodegaId : 0;
        var costos = await db.CostStates.AsNoTracking().Where(c => productos.Contains(c.ProductId) && c.ScopeWarehouseId == ambitoId)
            .ToDictionaryAsync(c => c.ProductId, c => c.Quantity > 0m ? c.AverageCost : c.LastUnitCost, ct);
        var ultimoKardex = await db.KardexEntries.AsNoTracking().MaxAsync(k => (long?)k.Id, ct) ?? 0L;

        foreach (var f in filas)
        {
            db.CountSnapshotLines.Add(new CountSnapshotLine
            {
                DocumentId = conteo.Id,
                ProductId = f.ProductId,
                LocationId = f.LocationId,
                LotId = f.LotId,
                TheoreticalQuantity = f.Quantity,
                SnapshotUnitCost = costos.GetValueOrDefault(f.ProductId),
            });
        }

        var ahora = reloj.UtcNow;
        conteo.OperationDate = hoy;
        conteo.CountSnapshotAt = ahora;
        conteo.CountSnapshotKardexEntryId = ultimoKardex;
        conteo.CountRound = 1;
        conteo.CountScopeJson = (criterio with { BloqueaMovimientos = leidos.Value.BloquearMovimientos, AbiertoPor = usuario }).ComoJson();
        await db.SaveChangesAsync(ct);

        return Result.Success(new OpenPhysicalCountResultDto(conteo.PublicId, ahora, hoy, filas.Count, leidos.Value.BloquearMovimientos));
    }

    private static Result<OpenPhysicalCountResultDto> Falla(Error error) => Result.Failure<OpenPhysicalCountResultDto>(error);
}
