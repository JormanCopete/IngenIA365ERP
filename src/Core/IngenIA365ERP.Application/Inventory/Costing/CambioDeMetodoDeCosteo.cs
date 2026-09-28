using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Costing;

/// <summary>
/// El cambio de método o de ámbito de costeo (feature 012, I5, US16, T841; FR-043; data-model §3.1, §3.5; decisiones-transversales
/// T21, T42b, T42g). Lo invoca <c>AddParameterVersionCommand</c> al registrar <c>Costeo.Metodo</c> o <c>Costeo.Ambito</c> —la única vía—,
/// después de que <c>ReglasDePlataformaDeInventario</c> comprobó el inicio de un período abierto sin movimientos posteriores
/// (<c>Parameters.RequiresPeriodStart</c>), que la vigencia no es futura (<c>Inventory.Costing.MethodChangeInFuture</c>) y, por la
/// definición de la clave, <c>Inventory.Costing.Manage</c> (<c>Parameters.PermissionRequired</c>); la justificación es el <c>reason</c>
/// obligatorio del comando y el acta va en <c>LegalSource</c>, exigida sólo con <c>Costeo.CambioExigeActa</c> (T842,
/// <c>Parameters.LegalSourceRequired</c>).
///
/// <para>
/// En la misma transacción genera un documento <c>CostAdjustment</c> del sistema, fechado en <c>validFrom</c>, confirmado y numerado, con
/// una línea por producto, y lo escribe por <see cref="RegistroDeKardex.RegistrarCambioDeMetodoAsync"/> —el único escritor—:
/// </para>
/// <list type="bullet">
/// <item><b>método</b>: por cada ámbito con existencia o valor, una línea <c>MethodChange</c> sin cantidad
/// (<see cref="MotorDeCosteo.CambiarMetodo"/>); a PEPS abre una sola capa con toda la existencia al promedio y la línea lleva sólo la
/// diferencia de redondeo; a promedio, cierra las capas;</item>
/// <item><b>ámbito</b>: la existencia de cada bodega y ubicación pasa del ámbito anterior al nuevo con un par de líneas <c>MethodChange</c>
/// (salida del anterior, entrada al nuevo) al promedio del anterior —el valor por bodega es cantidad × promedio del ámbito (T18); el
/// centavo que no reparte el redondeo va a la última—; con PEPS, cada ámbito nuevo abre su capa única como en el cambio de método.</item>
/// </list>
/// <para>No emite mensajes (mensajes.md §6.10): el valorizado por los dos métodos (<c>method-change-valuation</c>) es lo que se entrega a
/// Contabilidad. (nuevo)</para>
/// </summary>
public sealed class CambioDeMetodoDeCosteo(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    RegistroDeKardex registro,
    Numerador numerador,
    ICerrojoDeInventario cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IEfectoDeAltaDeParametro
{
    public async Task<Result> AplicarAsync(AltaDeParametro alta, string motivo, string? fuenteLegal, CancellationToken ct)
    {
        var clave = alta.Definicion.Clave;
        if (alta.Definicion.Modulo != ParametrosDeInventario.Modulo
            || clave is not (ParametrosDeInventario.CosteoMetodo or ParametrosDeInventario.CosteoAmbito))
            return Result.Success();

        // T842: el acta, sólo si la cooperativa la exige.
        var exigeActa = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoCambioExigeActa, alta.ValidFrom, ct: ct);
        if (exigeActa.IsFailure) return Result.Failure(exigeActa.Error);
        if (exigeActa.Value.Como<bool>() && string.IsNullOrWhiteSpace(fuenteLegal))
            return Result.Failure(ErroresDeParametros.ActaRequerida(clave));

        // El régimen anterior es el vigente la víspera (la vigencia nueva todavía no está guardada).
        var vispera = alta.ValidFrom.AddDays(-1);
        var anterior = await registro.LeerParametrosAsync(vispera, [], ct);
        if (anterior.IsFailure) return Result.Failure(anterior.Error);
        var (metodoAnterior, ambitoAnterior, montos) = (anterior.Value.Metodo, anterior.Value.Ambito, anterior.Value.Montos);
        var metodo = clave == ParametrosDeInventario.CosteoMetodo ? (alta.Valor == "Peps" ? CostMethod.Fifo : CostMethod.WeightedAverage) : metodoAnterior;
        var ambito = clave == ParametrosDeInventario.CosteoAmbito ? (alta.Valor == "Bodega" ? CostScope.Warehouse : CostScope.Cooperative) : ambitoAnterior;
        if (metodo == metodoAnterior && ambito == ambitoAnterior) return Result.Success();

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDelDocumento.SinUsuario());
        var tipo = await db.InventoryDocumentTypes.AsNoTracking()
            .Where(t => t.Class == DocumentClass.CostAdjustment && t.IsActive).OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
        if (tipo is null) return Result.Failure(InventoryErrors.DocumentTypeNotFound());
        var sucursal = await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (sucursal is null) return Result.Failure(InventoryErrors.DocumentTypeNotFound());

        // Lo que cambia: los estados del régimen anterior, sus capas vivas y la existencia por bodega y ubicación.
        var estados = await db.CostStates.AsNoTracking()
            .Where(c => ambitoAnterior == CostScope.Warehouse ? c.ScopeWarehouseId != 0 : c.ScopeWarehouseId == 0)
            .Where(c => c.Quantity != 0m || c.Value != 0m)
            .ToListAsync(ct);
        var productos = estados.Select(e => e.ProductId).Distinct().ToList();
        var capas = await db.CostLayers.AsNoTracking().Where(c => productos.Contains(c.ProductId) && c.RemainingQuantity > 0m).ToListAsync(ct);
        var detalles = (await db.StockDetails.AsNoTracking().Where(d => productos.Contains(d.ProductId) && d.Quantity != 0m).ToListAsync(ct))
            .GroupBy(d => (d.ProductId, d.WarehouseId, d.LocationId))
            .Select(g => (g.Key.ProductId, g.Key.WarehouseId, g.Key.LocationId, Cantidad: g.Sum(d => d.Quantity)))
            .Where(d => d.Cantidad != 0m)
            .OrderBy(d => d.ProductId).ThenBy(d => d.WarehouseId).ThenBy(d => d.LocationId)
            .ToList();
        var ubicacionPorDefecto = await db.WarehouseLocations.AsNoTracking().Where(l => l.IsDefault && l.IsActive)
            .GroupBy(l => l.WarehouseId).Select(g => new { g.Key, Id = g.Min(l => l.Id) }).ToDictionaryAsync(x => x.Key, x => x.Id, ct);
        var unidades = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.BaseUnitId, ct);

        var documento = new InventoryDocument
        {
            Class = DocumentClass.CostAdjustment,
            DocumentTypeId = tipo.Id,
            OperationDate = alta.ValidFrom,
            BranchId = sucursal.Value,
            CreatedByUserId = usuario,
            Reason = Recortar($"Cambio de {clave} a {alta.Valor}", 500),
            Notes = Recortar(fuenteLegal is null ? motivo : $"{motivo} (acta: {fuenteLegal})", 2000),
        };
        var numero = 0;
        var lineaDe = new Dictionary<int, InventoryDocumentLine>();
        foreach (var producto in productos)
        {
            var linea = new InventoryDocumentLine { Document = documento, LineNumber = ++numero, ProductId = producto, UnitId = unidades[producto], Quantity = 0m, QuantityBase = 0m };
            documento.Lines.Add(linea);
            lineaDe[producto] = linea;
        }
        db.InventoryDocuments.Add(documento);
        // Las líneas del kardex nombran el documento y sus líneas por Id: se guardan antes, dentro de la transacción del comando.
        await db.SaveChangesAsync(ct);

        var lineas = new List<LineaDeCambioDeMetodo>();
        var finales = new Dictionary<(int ProductId, int Ambito), EstadoDeCosto>();
        foreach (var estado in estados.OrderBy(e => e.ProductId).ThenBy(e => e.ScopeWarehouseId))
        {
            var actual = new EstadoDeCosto(estado.Quantity, estado.Value, estado.AverageCost, estado.LastUnitCost)
            {
                Capas = capas.Where(c => c.ProductId == estado.ProductId && c.ScopeWarehouseId == estado.ScopeWarehouseId)
                    .OrderBy(c => c.OperationDate).ThenBy(c => c.EntryKardexEntryId)
                    .Select(c => new CapaDeCosto(ReferenciaDeKardex.A(c.EntryKardexEntryId), c.OperationDate, c.OriginalQuantity, c.RemainingQuantity, c.UnitCost))
                    .ToList(),
            };
            var deProducto = detalles.Where(d => d.ProductId == estado.ProductId
                && (ambitoAnterior == CostScope.Cooperative || d.WarehouseId == estado.ScopeWarehouseId)).ToList();
            var linea = lineaDe[estado.ProductId];

            if (ambito == ambitoAnterior)
            {
                // Cambio de método: una línea MethodChange sin cantidad y, a PEPS, la capa única al promedio.
                var cambio = MotorDeCosteo.CambiarMetodo(actual, metodo, montos, alta.ValidFrom);
                var (bodega, ubicacion) = DondeAnotar(estado.ScopeWarehouseId, deProducto, ubicacionPorDefecto);
                var propuesta = cambio.Lineas[0];
                lineas.Add(new LineaDeCambioDeMetodo(linea, bodega, ubicacion, estado.ScopeWarehouseId, 0m, propuesta.UnitCost, propuesta.TotalCost,
                    cambio.CapasNuevas.FirstOrDefault()));
                finales[(estado.ProductId, estado.ScopeWarehouseId)] = cambio.Estado;
                continue;
            }

            // Cambio de ámbito: la existencia de cada bodega y ubicación pasa al ámbito nuevo al promedio del anterior.
            var costo = actual.CostoVigente;
            var partes = deProducto.Count > 0
                ? deProducto.Select(d => (d.WarehouseId, d.LocationId, d.Cantidad, Valor: Redondeo.Monto(d.Cantidad * costo, montos))).ToList()
                : [(DondeAnotar(estado.ScopeWarehouseId, [], ubicacionPorDefecto).Bodega, DondeAnotar(estado.ScopeWarehouseId, [], ubicacionPorDefecto).Ubicacion, 0m, 0m)];
            var ultima = partes[^1];
            partes[^1] = ultima with { Valor = ultima.Valor + (actual.Value - partes.Sum(x => x.Valor)) };
            foreach (var (bodega, ubicacion, cantidad, valor) in partes)
            {
                var nuevo = ambito == CostScope.Warehouse ? bodega : 0;
                lineas.Add(new LineaDeCambioDeMetodo(linea, bodega, ubicacion, estado.ScopeWarehouseId, -cantidad, costo, -valor));
                lineas.Add(new LineaDeCambioDeMetodo(linea, bodega, ubicacion, nuevo, cantidad, costo, valor));
                var previo = finales.GetValueOrDefault((estado.ProductId, nuevo), EstadoDeCosto.Vacio);
                finales[(estado.ProductId, nuevo)] = EstadoDeCosto.Con(previo.Quantity + cantidad, previo.Value + valor, actual.LastUnitCost);
            }
            finales[(estado.ProductId, estado.ScopeWarehouseId)] = EstadoDeCosto.Con(0m, 0m, actual.LastUnitCost);
        }

        // Con PEPS, cada ámbito nuevo de un cambio de ámbito abre su capa única al promedio, como un cambio de método.
        if (ambito != ambitoAnterior && metodo == CostMethod.Fifo)
        {
            foreach (var clavesDelNuevo in finales.Where(f => f.Value.Quantity > 0m && lineas.Any(l => l.Linea.ProductId == f.Key.ProductId && l.Ambito == f.Key.Ambito && l.QuantityBase > 0m)).ToList())
            {
                var (producto, ambitoNuevo) = clavesDelNuevo.Key;
                var cambio = MotorDeCosteo.CambiarMetodo(clavesDelNuevo.Value, CostMethod.Fifo, montos, alta.ValidFrom);
                var entrada = lineas.First(l => l.Linea.ProductId == producto && l.Ambito == ambitoNuevo && l.QuantityBase > 0m);
                lineas.Add(new LineaDeCambioDeMetodo(entrada.Linea, entrada.WarehouseId, entrada.LocationId, ambitoNuevo, 0m,
                    cambio.Lineas[0].UnitCost, cambio.Lineas[0].TotalCost, cambio.CapasNuevas.FirstOrDefault()));
                finales[(producto, ambitoNuevo)] = cambio.Estado;
            }
        }

        await cerrojo.BloquearAsync(RegistroDeKardex.CerrojoDelCambio(lineas, metodo), ct);
        await registro.RegistrarCambioDeMetodoAsync(documento, lineas, metodo, finales, ct);

        var numerado = await numerador.NumerarAsync(documento, tipo.Code, ct);
        if (numerado.IsFailure) return numerado;
        documento.Confirmar(usuario, reloj.UtcNow);
        return Result.Success();
    }

    /// <summary>La bodega y ubicación donde se anota la línea de un ámbito: la bodega del ámbito, o la de más existencia del producto.</summary>
    private static (int Bodega, int Ubicacion) DondeAnotar(
        int ambito, IReadOnlyList<(int ProductId, int WarehouseId, int LocationId, decimal Cantidad)> detalles, IReadOnlyDictionary<int, int> porDefecto)
    {
        if (detalles.Count > 0)
        {
            var mayor = detalles.Where(d => ambito == 0 || d.WarehouseId == ambito).OrderByDescending(d => d.Cantidad).ThenBy(d => d.WarehouseId).FirstOrDefault();
            if (mayor.WarehouseId != 0) return (mayor.WarehouseId, mayor.LocationId);
        }
        if (ambito != 0 && porDefecto.TryGetValue(ambito, out var ubicacion)) return (ambito, ubicacion);
        var primera = porDefecto.OrderBy(x => x.Key).First();
        return (primera.Key, primera.Value);
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
