using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Transfers;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>TransferDispatch</c> (feature 012, US10, T367; FR-039, US10-1; contracts/api.md §11; mensajes.md §6.6).
/// (nuevo)
/// <list type="bullet">
/// <item>el origen es una bodega operativa del alcance (el tránsito nunca despacha: <c>Inventory.Document.TransitNotAllowed</c>,
/// regla común); el destino, cualquier bodega operativa y activa de la cooperativa (<c>Inventory.Warehouse.NotActive</c>) distinta
/// del origen (<c>Inventory.Transfer.SameWarehouse</c>);</item>
/// <item>resuelve la bodega de tránsito de la <b>sucursal de origen</b> y la guarda en <c>TransitWarehouseId</c>
/// (<c>Inventory.Transfer.TransitWarehouseMissing</c> si no hay);</item>
/// <item>cada línea sale del origen al costo vigente y entra al tránsito <b>al mismo costo</b> (<see cref="MovimientoDeKardex.AlCostoDe"/>):
/// el valor total no cambia; el destino la ve «en tránsito» y no disponible (<c>PosicionDeReposicion</c>);</item>
/// <item>el monto que se aprueba es el valor al costo de lo que sale;</item>
/// <item>emite <c>TrasladoDespachado</c> v1 y sella el modo de la cadena <c>Transfers</c> (lo hace el ciclo común: no es derivado);</item>
/// <item>anular un despacho no recibido devuelve del tránsito al origen al costo del despacho (anulación neutra); con recepción, el
/// ciclo común responde <c>Inventory.Document.HasDependents</c>.</item>
/// </list>
/// Es <c>Scoped</c>: lo preparado se recuerda por documento dentro de la petición.
/// </summary>
public sealed class EfectoDespachoDeTraslado(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos, decimal Monto)> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];

    public override DocumentClass Clase => DocumentClass.TransferDispatch;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var avisos = new List<Error>(await ReglasAsync(contexto.Documento, ct));
        avisos.AddRange(await ReglasDeTraslado.ProductosAsync(contexto.Documento, maestros, admiteBloqueado: false, ct));
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
            var reglas = await ReglasAsync(documento, ct);
            if (reglas.Count > 0) return Result.Failure(reglas[0]);
            var productos = await ReglasDeTraslado.ProductosAsync(documento, maestros, admiteBloqueado: false, ct);
            if (productos.Count > 0) return Result.Failure(productos[0]);

            var origen = (await maestros.BodegasPorIdAsync([documento.WarehouseId!.Value], ct)).First();
            var transito = await ReglasDeTraslado.TransitoDeLaSucursalAsync(db, origen.BranchId, ct);
            if (transito is null) return Result.Failure(ErroresDeTraslados.TransitWarehouseMissing(origen.Code));
            documento.TransitWarehouseId = transito;

            var ubicacion = (await ReglasDeTraslado.UbicacionesPorDefectoAsync(db, [transito.Value], ct)).GetValueOrDefault(transito.Value);
            var lista = new List<MovimientoDeKardex>();
            foreach (var linea in documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber))
            {
                var sale = new MovimientoDeKardex(linea, origen.Id, -linea.QuantityBase, ValoracionDelMovimiento.AlCostoVigente, LocationId: linea.LocationId);
                lista.Add(sale);
                lista.Add(new MovimientoDeKardex(linea, transito.Value, linea.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen,
                    LocationId: ubicacion == 0 ? null : ubicacion, AlCostoDe: sale));
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
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registrados[contexto.Documento.PublicId] = registrado.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var filas = _registrados.TryGetValue(contexto.Documento.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == contexto.Documento.Id).ToListAsync(ct);
        if (filas.Count == 0) return [];
        return [await emision.TrasladoDespachadoAsync(contexto.Documento, filas, ct)];
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (!_preparados.TryGetValue(contexto.Documento.PublicId, out var p)) return Result.Success();
        var registrado = await registro.RegistrarAsync(contexto.Documento, p.Movimientos, ct, ReversionDeKardex.SugerenciaAjusteNegativo);
        return registrado.IsFailure ? Result.Failure(registrado.Error) : Result.Success();
    }

    public override Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        emision.AnulacionAsync(contexto.Documento, contexto.Original!, [], ct);

    /// <summary>Destino obligatorio, distinto del origen, operativo y activo.</summary>
    private async Task<IReadOnlyList<Error>> ReglasAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (documento.DestinationWarehouseId is not int destinoId) return [InventoryErrors.FieldRequired(ErroresDeTraslados.CampoDestino)];
        var bodegas = (await maestros.BodegasPorIdAsync(new[] { documento.WarehouseId, destinoId }.OfType<int>().Distinct().ToList(), ct)).ToDictionary(b => b.Id);
        if (!bodegas.TryGetValue(destinoId, out var destino)) return [InventoryErrors.FieldRequired(ErroresDeTraslados.CampoDestino)];
        if (documento.WarehouseId == destinoId) return [ErroresDeTraslados.SameWarehouse(destino.Code)];
        if (destino.Inactiva) return [InventoryErrors.WarehouseInactive(destino.PublicId, destino.Code)];
        if (destino.EsTransito || !destino.Activa) return [InventoryErrors.WarehouseNotActive(destino.PublicId, destino.Code)];
        return [];
    }
}
