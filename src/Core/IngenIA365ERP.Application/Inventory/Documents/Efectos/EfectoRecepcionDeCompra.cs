using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>PurchaseReceipt</c> (feature 012, T340; FR-044, FR-049; contracts/api.md §14.2; mensajes.md §6.3). (nuevo)
/// <list type="bullet">
/// <item>reglas: proveedor obligatorio; productos inventariables, activos y no bloqueados; nunca el tránsito;</item>
/// <item>el costo de entrada de cada línea lo da <see cref="CostoDeEntrada.DeCompra"/>: neto de descuentos no condicionados más
/// los impuestos que van al costo (el IVA no descontable —cooperativa no responsable, tipo con <c>VatNonDeductible</c>,
/// producto de venta excluida— y el INC), con los impuestos que calcula <see cref="CalculoTributarioDeCompra"/> a la fecha;</item>
/// <item>entra al kardex al costo de entrada por <see cref="RegistroDeKardex"/> (tres cajas × 12 son 36 unidades en unidad base);</item>
/// <item>deja la foto de los impuestos <c>AddedToCost</c> (<c>INV_DocumentTaxLines</c>): son parte de su costo;</item>
/// <item>emite <c>CompraRecibida</c> v1 y sella el modo de la cadena <c>Purchases</c> (lo hace el ciclo común: la recepción es la
/// raíz de la cadena);</item>
/// <item>el monto que se aprueba es el <c>Total</c> (§1.3);</item>
/// <item>anularla revierte al costo con que entró (<see cref="ReversionDeKardex"/>); con factura o devolución vigente el ciclo
/// común responde <c>Inventory.Document.HasDependents</c>.</item>
/// </list>
/// Scoped: lo preparado y lo registrado se recuerdan por documento dentro de la petición.
/// </summary>
public sealed class EfectoRecepcionDeCompra(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos, CalculoDeCompra Calculo)> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];
    private readonly Dictionary<Guid, ReversionHecha> _revertidos = [];

    public override DocumentClass Clase => DocumentClass.PurchaseReceipt;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        await ReglasDeCompra.ProductosDeMercanciaAsync(contexto.Documento, maestros, ct);

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (contexto.EsAnulacion)
        {
            var reversa = await reversion.MovimientosAsync(documento, contexto.Original!, ct);
            if (reversa.Count == 0) return Result.Success();
            var prep = await registro.PrepararAsync(documento, reversa, ct);
            if (prep.IsFailure) return Result.Failure(prep.Error);
            _preparados[documento.PublicId] = (prep.Value, reversa, new CalculoDeCompra([], TotalesDeCompra.Cero, [], false));
            return Result.Success();
        }

        var reglas = await ReglasDeCompra.ComunesAsync(contexto, maestros, ct);
        if (reglas is not null) return Result.Failure(reglas);
        var productos = await ReglasDeCompra.ProductosDeMercanciaAsync(documento, maestros, ct);
        if (productos.Count > 0) return Result.Failure(productos[0]);

        var calculado = await calculo.CalcularAsync(documento, contexto.Tipo, ct);
        if (calculado.IsFailure) return Result.Failure(calculado.Error);
        CalculoTributarioDeCompra.AplicarTotales(documento, calculado.Value.Totales);

        var movimientos = await MovimientosAsync(documento, contexto.Tipo, calculado.Value, ct);
        if (movimientos.IsFailure) return Result.Failure(movimientos.Error);
        var preparado = await registro.PrepararAsync(documento, movimientos.Value, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[documento.PublicId] = (preparado.Value, movimientos.Value, calculado.Value);
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        contexto.EsAnulacion && _preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Preparacion.ValorEstimado : contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var (_, movimientos, calculado) = _preparados[documento.PublicId];
        var registrado = await registro.RegistrarAsync(documento, movimientos, ct);
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registrados[documento.PublicId] = registrado.Value;

        // La foto de lo que sumó al costo (research R20): la factura cancela la cuenta puente con el mismo valor.
        var lineas = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber);
        foreach (var r in calculado.Renglones.Where(r => r.Treatment == TaxTreatment.AddedToCost && r.Linea is int n && lineas.ContainsKey(n)))
            db.DocumentTaxLines.Add(CalculoTributarioDeCompra.Foto(documento.Id, lineas[r.Linea!.Value].Id, r));
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var filas = _registrados.TryGetValue(contexto.Documento.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await ReglasDeCompra.KardexDelDocumentoAsync(db, contexto.Documento, ct);
        return filas.Count == 0 ? [] : [await emision.CompraRecibidaAsync(contexto.Documento, filas, ct)];
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var revertido = await reversion.RevertirAsync(contexto.Documento, contexto.Original!, ct);
        if (revertido.IsFailure) return Result.Failure(revertido.Error);
        _revertidos[contexto.Documento.PublicId] = revertido.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var diferencias = _revertidos.TryGetValue(contexto.Documento.PublicId, out var hecha) ? hecha.Diferencias : [];
        return await emision.AnulacionAsync(contexto.Documento, contexto.Original!, diferencias, ct);
    }

    /// <summary>Un movimiento de entrada por línea, al costo de entrada de la compra.</summary>
    private async Task<Result<IReadOnlyList<MovimientoDeKardex>>> MovimientosAsync(
        InventoryDocument documento, InventoryDocumentType tipo, CalculoDeCompra calculado, CancellationToken ct)
    {
        if (documento.WarehouseId is not int bodega) return Result.Success<IReadOnlyList<MovimientoDeKardex>>([]);
        var parametros = await registro.LeerParametrosAsync(documento.OperationDate, [bodega], ct);
        if (parametros.IsFailure) return Result.Failure<IReadOnlyList<MovimientoDeKardex>>(parametros.Error);

        var vivas = documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber).ToList();
        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var tratamientos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.VatSaleTreatment, ct);

        var movimientos = new List<MovimientoDeKardex>(vivas.Count);
        foreach (var linea in vivas)
        {
            var costo = CostoDeEntrada.DeCompra(new PedidoDeCostoDeCompra(
                linea.QuantityBase,
                linea.GrossAmount,
                linea.DiscountAmount,
                calculado.DeLinea(linea.LineNumber).Select(r => new ImpuestoDeCompra(r.Kind, r.Amount, r.TaxRateCode)).ToList(),
                calculado.CooperativaResponsableIva,
                tipo.VatNonDeductible,
                tratamientos.GetValueOrDefault(linea.ProductId, VatSaleTreatment.Taxed)), parametros.Value.Montos);
            movimientos.Add(new MovimientoDeKardex(linea, bodega, linea.QuantityBase, ValoracionDelMovimiento.AlCostoIndicado, costo.CostoUnitario,
                LocationId: linea.LocationId));
        }
        return Result.Success<IReadOnlyList<MovimientoDeKardex>>(movimientos);
    }
}

/// <summary>Las reglas que comparten las estrategias de compras (US9). (nuevo)</summary>
public static class ReglasDeCompra
{
    /// <summary>Proveedor obligatorio y ningún documento de compras en el tránsito. Nulo si procede.</summary>
    public static async Task<Error?> ComunesAsync(ContextoDeEfecto contexto, IMaestrosDelDocumento maestros, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (documento.CounterpartyPersonId is null) return InventoryErrors.FieldRequired(ReglasDelDocumento.CampoContraparte);
        if (documento.WarehouseId is int b && (await maestros.BodegasPorIdAsync([b], ct)).FirstOrDefault() is { EsTransito: true } transito)
            return InventoryErrors.TransitNotAllowed(transito.Code);
        return null;
    }

    /// <summary>Las líneas que mueven mercancía: producto inventariable, activo y no bloqueado.</summary>
    public static async Task<IReadOnlyList<Error>> ProductosDeMercanciaAsync(InventoryDocument documento, IMaestrosDelDocumento maestros, CancellationToken ct)
    {
        var errores = new List<Error>();
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productos = (await maestros.ProductosPorIdAsync(vivas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var linea in vivas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            if (!producto.Inventariable) errores.Add(InventoryErrors.ProductNotInventoriable(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Blocked) errores.Add(InventoryErrors.ProductBlocked(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Inactive) errores.Add(InventoryErrors.ProductInactive(linea.LineNumber, producto.Code));
        }
        return errores;
    }

    /// <summary>El kardex del documento, en memoria (recién escrito) o en la base.</summary>
    public static async Task<List<KardexEntry>> KardexDelDocumentoAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct)
    {
        var enMemoria = db.KardexEntries.Local.Where(k => k.DocumentId == documento.Id).ToList();
        return enMemoria.Count > 0 ? enMemoria : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).ToListAsync(ct);
    }
}
