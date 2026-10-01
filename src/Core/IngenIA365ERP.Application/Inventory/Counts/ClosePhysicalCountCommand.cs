using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// Cierra un conteo físico (feature 012, US11, T395; FR-040, US11-3; contracts/api.md §12, <c>POST /counts/{id}/close</c> con
/// <c>{ rowVersion }</c>, permiso <c>Inventory.Counts.Close</c>): termina la captura, compara con <c>ComparacionDeConteo</c> y confirma
/// el conteo por el flujo canónico —clase <c>PhysicalCount</c>: sin kardex ni mensajes— con su número, que se asigna aquí (FR-038:
/// un conteo descartado no consumió ninguno). (nuevo)
/// </summary>
public sealed record ClosePhysicalCountCommand(Guid CountPublicId) : IRequest<Result<ClosePhysicalCountResultDto>>, IOperacionIdempotente
{
    public byte[]? RowVersion { get; init; }

    public Guid OperationKey { get; init; }
}

public sealed class ClosePhysicalCountCommandValidator : AbstractValidator<ClosePhysicalCountCommand>
{
    public ClosePhysicalCountCommandValidator() => RuleFor(x => x.CountPublicId).NotEmpty();
}

/// <summary>
/// Dentro de una <see cref="TransaccionExplicita"/>, con la bodega y la fila del conteo bloqueadas (ninguna tanda entra mientras se
/// cierra): el conteo existe en el alcance (404) y está abierto (<c>Inventory.Count.NotOpen</c>); una línea con diferencia fuera de
/// tolerancia sin su reconteo responde <c>Inventory.Count.RecountRequired</c> con <c>data.lines[]</c>. Si no, fija en cada línea lo
/// movido después de la foto (si el conteo admitía movimientos), lo contado en la ronda que manda y la diferencia, y confirma por
/// <see cref="ConfirmacionDeDocumento"/> (que numera y guarda todo junto).
/// </summary>
public sealed class ClosePhysicalCountCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeInventario cerrojo,
    VistaDeConteos conteos,
    VistaDeDocumentos vista,
    ConfirmacionDeDocumento confirmacion)
    : IRequestHandler<ClosePhysicalCountCommand, Result<ClosePhysicalCountResultDto>>
{
    public Task<Result<ClosePhysicalCountResultDto>> Handle(ClosePhysicalCountCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => CerrarAsync(request, ct), ct);

    private async Task<Result<ClosePhysicalCountResultDto>> CerrarAsync(ClosePhysicalCountCommand request, CancellationToken ct)
    {
        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: true, ct);
        if (conteo is null) return Falla(ErroresDeConteos.NotFound());
        if (!VistaDeConteos.EstaAbierto(conteo)) return Falla(ErroresDeConteos.NotOpen());
        if (request.RowVersion is { Length: > 0 } leida && conteo.RowVersion is { Length: > 0 } actual && !leida.AsSpan().SequenceEqual(actual))
            return Falla(Error.StaleRowVersion);

        await cerrojo.BloquearAsync(new PedidoDeCerrojo { Bodegas = conteo.WarehouseId is int b ? [b] : [], DocumentosDeOrigen = [conteo.Id] }, ct);

        var lineas = await db.CountSnapshotLines.Where(l => l.DocumentId == conteo.Id).OrderBy(l => l.Id).ToListAsync(ct);
        var comparadas = await conteos.CompararAsync(conteo, lineas, ct);
        if (comparadas.IsFailure) return Falla(comparadas.Error);

        var porRecontar = comparadas.Value.Where(c => c.RequiereReconteo).ToList();
        if (porRecontar.Count > 0)
        {
            var ids = porRecontar.Select(c => lineas.First(l => l.Id == c.Linea)).ToList();
            var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Select(l => l.ProductId).Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new { p.PublicId, p.Code }, ct);
            var ubicaciones = await db.WarehouseLocations.AsNoTracking().IgnoreQueryFilters().Where(u => ids.Select(l => l.LocationId).Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.PublicId, u.Code }, ct);
            return Falla(ErroresDeConteos.RecountRequired(porRecontar.Select(c =>
            {
                var l = lineas.First(x => x.Id == c.Linea);
                var p = productos.GetValueOrDefault(l.ProductId);
                var u = ubicaciones.GetValueOrDefault(l.LocationId);
                return new ErroresDeConteos.LineaPorRecontar(p?.PublicId ?? Guid.Empty, p?.Code ?? string.Empty, u?.PublicId ?? Guid.Empty,
                    u?.Code ?? string.Empty, c.Teorico, c.Contado, c.Diferencia);
            }).ToList()));
        }

        foreach (var c in comparadas.Value)
            lineas.First(l => l.Id == c.Linea).Cerrar(c.MovimientosPosteriores, c.Contado, c.Diferencia, c.RondaQueManda);

        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(conteo.PublicId, DocumentClassGroup.Counts), ct);
        if (confirmada.IsFailure) return Falla(confirmada.Error);

        var r = confirmada.Value;
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var conDiferencia = lineas.Where(l => (l.Difference ?? 0m) != 0m).ToList();
        return Result.Success(new ClosePhysicalCountResultDto(conteo.PublicId,
            r.Status == DocumentStatus.Confirmed ? EstadosDeConteo.Cerrado : r.Status.ToString(),
            r.Number, r.DisplayNumber, lineas.Count, conDiferencia.Count,
            costos ? conDiferencia.Sum(l => Math.Round(l.Difference!.Value * l.SnapshotUnitCost, 2, MidpointRounding.AwayFromZero)) : null));
    }

    private static Result<ClosePhysicalCountResultDto> Falla(Error error) => Result.Failure<ClosePhysicalCountResultDto>(error);
}
