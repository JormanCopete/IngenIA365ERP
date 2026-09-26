using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Transfers;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>LocationMove</c> (feature 012, US10, T368; US10-4; contracts/api.md §10), sobre la ruta de ajustes de US2:
/// dentro de una bodega, un paso, de <c>LocationId</c> (o la ubicación por defecto) a <c>ToLocationId</c> (obligatoria,
/// <c>Inventory.Document.FieldRequired</c> con <c>toLocation</c>), nunca a la misma (<c>Inventory.Location.Same</c>). Cada línea deja
/// dos filas de kardex de valor igual y signo contrario en la misma bodega: el estado de costo no cambia —ni el promedio ni el
/// último costo— y no emite mensajes (su clase no tiene; el modo de paso queda nulo). Anularlo devuelve la mercancía a su
/// ubicación de origen de la misma forma. (nuevo)
/// </summary>
public sealed class EfectoMovimientoEntreUbicaciones(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    IMaestrosDelDocumento maestros,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos, decimal Monto)> _preparados = [];

    public override DocumentClass Clase => DocumentClass.LocationMove;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var avisos = new List<Error>(await ReglasDeTraslado.ProductosAsync(contexto.Documento, maestros, admiteBloqueado: false, ct));
        avisos.AddRange(await UbicacionesAsync(contexto.Documento, ct));
        return avisos;
    }

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        IReadOnlyList<MovimientoDeKardex> movimientos;
        if (contexto.EsAnulacion)
        {
            movimientos = await ReglasDeTraslado.ReversionNeutraAsync(reversion, documento, contexto.Original!, ct);
        }
        else
        {
            var productos = await ReglasDeTraslado.ProductosAsync(documento, maestros, admiteBloqueado: false, ct);
            if (productos.Count > 0) return Result.Failure(productos[0]);
            var ubicaciones = await UbicacionesAsync(documento, ct);
            if (ubicaciones.Count > 0) return Result.Failure(ubicaciones[0]);
            if (documento.WarehouseId is not int bodega) return Result.Failure(InventoryErrors.FieldRequired(ReglasDelDocumento.CampoBodega));

            var lista = new List<MovimientoDeKardex>();
            foreach (var linea in documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber))
            {
                var sale = new MovimientoDeKardex(linea, bodega, -linea.QuantityBase, ValoracionDelMovimiento.AlCostoVigente, LocationId: linea.LocationId);
                lista.Add(sale);
                lista.Add(new MovimientoDeKardex(linea, bodega, linea.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen,
                    LocationId: linea.ToLocationId, AlCostoDe: sale));
            }
            movimientos = lista;
        }

        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        var monto = await ReglasDeTraslado.ValorDeLasSalidasAsync(registro, documento, movimientos, ct);
        if (monto.IsFailure) return Result.Failure(monto.Error);
        _preparados[documento.PublicId] = (preparado.Value, movimientos, monto.Value);
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Monto : base.MontoParaAprobar(contexto);

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (!_preparados.TryGetValue(contexto.Documento.PublicId, out var p)) return Result.Success();
        var registrado = await registro.RegistrarAsync(contexto.Documento, p.Movimientos, ct);
        return registrado.IsFailure ? Result.Failure(registrado.Error) : Result.Success();
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (!_preparados.TryGetValue(contexto.Documento.PublicId, out var p)) return Result.Success();
        var registrado = await registro.RegistrarAsync(contexto.Documento, p.Movimientos, ct);
        return registrado.IsFailure ? Result.Failure(registrado.Error) : Result.Success();
    }

    /// <summary>Cada línea con su ubicación de destino, distinta de la de origen (la por defecto si la línea no trae).</summary>
    private async Task<IReadOnlyList<Error>> UbicacionesAsync(InventoryDocument documento, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        if (vivas.Any(l => l.ToLocationId is null)) return [InventoryErrors.FieldRequired(ErroresDeTraslados.CampoUbicacionDeDestino)];
        var porDefecto = documento.WarehouseId is int bodega
            ? (await ReglasDeTraslado.UbicacionesPorDefectoAsync(db, [bodega], ct)).GetValueOrDefault(bodega)
            : 0;
        var productos = (await maestros.ProductosPorIdAsync(vivas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var linea in vivas)
        {
            if ((linea.LocationId ?? porDefecto) == linea.ToLocationId)
                return [ErroresDeTraslados.LocationSame(linea.LineNumber, productos.GetValueOrDefault(linea.ProductId)?.Code ?? string.Empty)];
        }
        return [];
    }
}
