using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// Las notas de venta (feature 012, I3, T610, T612; FR-044, FR-066; mensajes.md §6.8, §6.11): <c>CreditNote</c>, <c>PosAdjustmentNote</c>
/// y <c>NonElectronicSalesNote</c>, una estrategia por clase sobre esta familia (T17).
/// <list type="bullet">
/// <item>antes de la aprobación: las reglas de la nota de <see cref="ReglasDeConfirmacionDeVenta"/> (veredicto fiscal, original y su clase,
/// lo que queda por acreditar, impuestos con la foto del original, reintegros); con devolución prepara la entrada;</item>
/// <item>dentro del cerrojo, sólo si devuelve mercancía (<c>ReturnsGoods</c>): la entrada <b>al costo con que salió</b> —el
/// <c>UnitCost</c> de la línea original enlazada—, aunque el promedio haya cambiado (<see cref="ValoracionDelMovimiento.AlCostoDeOrigen"/>);
/// sin devolución (descuento posterior) no toca la existencia; después la foto de impuestos, la liberación de los bonos reintegrados y el
/// toque de las sesiones;</item>
/// <item>es <b>derivada</b> de la venta que corrige: copia su modo de paso y sus mensajes la tienen como relacionada;</item>
/// <item>emite <c>NotaCreditoEmitida</c> y, con devolución, <c>DevolucionRegistrada</c> (<c>DevolucionDeCliente</c>, <c>Entry</c>);</item>
/// <item>su anulación —sólo la nota no electrónica— la resuelve <see cref="AnulacionDeVenta"/>.</item>
/// </list>
/// (nuevo)
/// </summary>
public abstract class DevolucionDeCliente(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos)> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await anulacion.ValidarAsync(contexto, ct);

        var nota = contexto.Documento;
        var productos = await ReglasDeLineasDeVenta.ProductosAsync(nota, maestros, ct);
        if (productos.IsFailure) return Result.Failure(productos.Error);
        var reglasDeNota = await reglas.ValidarNotaAsync(contexto, ct);
        if (reglasDeNota.IsFailure) return reglasDeNota;
        if (!nota.ReturnsGoods || nota.WarehouseId is not int bodega) return Result.Success();

        // Cada línea entra al costo con que salió su línea original (UnitCost que dejó el kardex de la venta).
        var vinculos = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => !x.IsDeleted && x.DocumentLink!.TargetDocumentId == nota.Id && x.DocumentLink.Kind == DocumentLinkKind.ReturnOf && !x.DocumentLink.IsDeleted)
            .Select(x => new { x.TargetLineId, x.SourceLine!.UnitCost }).ToListAsync(ct);
        var movimientos = nota.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m && productos.Value.Contains(l.ProductId)).OrderBy(l => l.LineNumber)
            .Select(l => new MovimientoDeKardex(l, bodega, l.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen,
                vinculos.FirstOrDefault(v => v.TargetLineId == l.Id)?.UnitCost ?? 0m, LocationId: l.LocationId))
            .ToList();
        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(nota, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[nota.PublicId] = (preparado.Value, movimientos);
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto)
    {
        if (contexto.EsAnulacion) return anulacion.Cerrojo(contexto, base.Cerrojo(contexto));
        return _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);
    }

    /// <summary>La nota deriva de la venta que corrige (data-model §5.3; FR-075): copia su modo y la nombra como relacionada.</summary>
    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return [];
        var original = await NotasDeVenta.OriginalDeAsync(db, contexto.Documento, ct);
        return original is { Status: DocumentStatus.Confirmed } ? [original] : [];
    }

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (_preparados.TryGetValue(contexto.Documento.PublicId, out var p))
        {
            var registrado = await registro.RegistrarAsync(contexto.Documento, p.Movimientos, ct);
            if (registrado.IsFailure) return Result.Failure(registrado.Error);
            _registrados[contexto.Documento.PublicId] = registrado.Value;
            contexto.Documento.CostTotal = registrado.Value.Lineas.Sum(k => k.TotalCost);
        }
        return await reglas.AlConfirmarNotaAsync(contexto, ct);
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var nota = contexto.Documento;
        var filas = _registrados.TryGetValue(nota.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await KardexAsync(nota, ct);
        return await ContenidosAsync(nota, filas, ct);
    }

    /// <summary>T520: la nota con la devolución al costo con que salió (ya conocido antes del cerrojo).</summary>
    public override async Task<IReadOnlyList<object>> MensajesProvisionalesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await MensajesDeAnulacionAsync(contexto, ct);
        var nota = contexto.Documento;
        IReadOnlyList<KardexEntry> filas = [];
        if (nota.WarehouseId is int bodega && _preparados.TryGetValue(nota.PublicId, out var p))
        {
            var costos = p.Movimientos.ToDictionary(m => m.Linea.Id, m => m.CostoUnitario);
            filas = (await registro.FilasProvisionalesAsync(nota, bodega, KardexEntryKind.Entry, l => costos.GetValueOrDefault(l.Id), ct))
                .Where(f => costos.ContainsKey(f.DocumentLineId)).ToList();
        }
        return await ContenidosAsync(nota, filas, ct);
    }

    public override Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.RevertirAsync(contexto, ct);

    public override Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.MensajesAsync(contexto, ct);

    private async Task<IReadOnlyList<object>> ContenidosAsync(InventoryDocument nota, IReadOnlyList<KardexEntry> filas, CancellationToken ct)
    {
        var impuestos = await reglas.ImpuestosDeAsync(nota, ct);
        var reintegros = await reglas.PagosDeAsync(nota, PaymentDirection.Refunded, ct);
        var contenidos = new List<object> { await emision.NotaCreditoAsync(nota, impuestos, reintegros, ct) };
        if (nota.ReturnsGoods && filas.Count > 0) contenidos.Add(await emision.DevolucionDeClienteAsync(nota, filas, ct));
        return contenidos;
    }

    private async Task<List<KardexEntry>> KardexAsync(InventoryDocument documento, CancellationToken ct)
    {
        var enMemoria = db.KardexEntries.Local.Where(k => k.DocumentId == documento.Id).ToList();
        return enMemoria.Count > 0 ? enMemoria : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).ToListAsync(ct);
    }
}

/// <summary><c>CreditNote</c>: la nota crédito electrónica de una factura; confirma con I4. (nuevo)</summary>
public sealed class EfectoNotaCreditoDeVenta(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db)
    : DevolucionDeCliente(registro, emision, maestros, reglas, anulacion, db)
{
    public override DocumentClass Clase => DocumentClass.CreditNote;
}

/// <summary><c>PosAdjustmentNote</c>: la nota de ajuste del documento equivalente POS; confirma con I4. (nuevo)</summary>
public sealed class EfectoNotaDeAjustePos(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db)
    : DevolucionDeCliente(registro, emision, maestros, reglas, anulacion, db)
{
    public override DocumentClass Clase => DocumentClass.PosAdjustmentNote;
}

/// <summary><c>NonElectronicSalesNote</c>: la nota del comprobante no electrónico; se anula con documento contrario. (nuevo)</summary>
public sealed class EfectoNotaDeVentaNoElectronica(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db)
    : DevolucionDeCliente(registro, emision, maestros, reglas, anulacion, db)
{
    public override DocumentClass Clase => DocumentClass.NonElectronicSalesNote;
}
