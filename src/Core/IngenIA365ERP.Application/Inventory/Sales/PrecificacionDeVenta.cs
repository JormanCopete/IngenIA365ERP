using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Pricing.Promotions;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Sales.Pricing;
using IngenIA365ERP.Domain.Sales.Promotions;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Una línea de venta que se precifica (nuevo). <see cref="PrecioDigitado"/> es el precio que la persona escribió en lugar del de
/// lista, en la misma base que la lista (con impuestos si la lista los incluye); bajo el de lista es un descuento
/// <c>IsPriceOverride</c>. <see cref="DescuentoPorcentaje"/> es fracción (0,05 = 5 %); <see cref="DescuentoValor"/> va en pesos, en la
/// base de la lista. <see cref="ListaFijada"/>: la línea ya tenía su lista y precio (un borrador que no cambió de cliente) y no se
/// vuelve a resolver.
/// </summary>
public sealed record LineaAPrecificar(
    int LineNumber,
    int ProductId,
    int UnitId,
    decimal Quantity,
    decimal QuantityBase,
    decimal? PrecioDigitado = null,
    decimal? DescuentoPorcentaje = null,
    decimal? DescuentoValor = null,
    PrecioFijado? ListaFijada = null);

/// <summary>El precio de lista que la línea ya guardó (<c>PriceListId</c>, <c>ListPrice</c>, <c>ListPriceIncludesTaxes</c>). (nuevo)</summary>
public sealed record PrecioFijado(int? PriceListId, decimal ListPrice, bool IncludesTaxes);

/// <summary>
/// Lo que se precifica (nuevo): la fecha de la operación, el comprador (su perfil tributario y su segmento), el canal (el del
/// punto en el POS, el del tipo en oficina), la sucursal y la bodega (costo y disponible), quién vende (su tope de descuento), las
/// líneas y el descuento por total en pesos sobre la suma de los netos sin impuestos.
/// </summary>
public sealed record PedidoDePrecificacion(
    DateOnly Fecha,
    int? CompradorPersonId,
    int? SalesChannelId,
    int? BranchId,
    int? WarehouseId,
    int? VendedorUserId,
    IReadOnlyList<LineaAPrecificar> Lineas,
    decimal? DescuentoPorTotal = null,
    string? MunicipioDane = null);

/// <summary>
/// Un descuento de la línea tal como irá a <c>INV_DocumentLineDiscounts</c> (nuevo). I6 (T875): <see cref="Source"/> =
/// <c>Promotion</c> con su <see cref="PromotionId"/> y la <see cref="Explicacion"/> del motor cuando lo produjo una promoción.
/// </summary>
public sealed record DescuentoCalculado(
    byte Sequence,
    bool FromDocumentDiscount,
    bool IsPriceOverride,
    decimal? Rate,
    decimal Amount,
    decimal CapRateApplied,
    bool RequiresApproval,
    DiscountSource Source = DiscountSource.Manual,
    int? PromotionId = null,
    string? Explicacion = null);

/// <summary>
/// Una línea precificada (nuevo). <see cref="UnitPrice"/> es siempre sin impuestos (18,6); con una lista que los incluye,
/// <c>ListPrice ÷ (1 + Σ tarifas porcentuales generadas)</c>. <see cref="Residuo"/> es lo que el redondeo dejó entre lo que dice
/// la lista con impuestos y bruto + impuestos, asignado por <c>Redondeo.Residuo</c> y ya sumado al bruto (FR-017).
/// <see cref="Available"/> es físico − reservado de la bodega en unidad base (nulo en un servicio o sin bodega).
/// </summary>
public sealed record LineaPrecificada(
    int LineNumber,
    int ProductId,
    int UnitId,
    decimal Quantity,
    decimal QuantityBase,
    int? PriceListId,
    decimal ListPrice,
    bool ListPriceIncludesTaxes,
    bool FallbackUsed,
    decimal UnitPrice,
    decimal GrossAmount,
    IReadOnlyList<DescuentoCalculado> Descuentos,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal Residuo,
    decimal? AverageCost,
    bool BelowCost,
    decimal? Available)
{
    public bool RequiereAprobacion => Descuentos.Any(d => d.RequiresApproval);
}

/// <summary>Los totales de la venta (T26): <c>Total = Subtotal − Descuentos + Impuestos</c>; <c>AmountDue = Total − Retenciones</c>. (nuevo)</summary>
public sealed record TotalesDeVenta(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue);

/// <summary>Lo que devuelve la precificación (nuevo). <see cref="Avisos"/> son textos para la persona (bajo costo con «Alertar», respaldo de lista).</summary>
public sealed record VentaPrecificada(
    IReadOnlyList<LineaPrecificada> Lineas,
    IReadOnlyList<RenglonTributario> Renglones,
    TotalesDeVenta Totales,
    TopeDelUsuario Tope,
    DescuentoPorTotal? DescuentoPorTotal,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<string> Omisiones)
{
    public bool RequiereAprobacion => Lineas.Any(l => l.RequiereAprobacion);
}

/// <summary>
/// La precificación de las líneas de una venta (feature 012, I3, T601; FR-053, FR-054, FR-017; data-model §14; T22, T26, T51): el
/// único sitio donde una línea de venta recibe precio, descuentos, impuestos y marca de bajo costo; lo usan el POS y el borrador de
/// oficina. En orden:
/// <list type="number">
/// <item>resuelve la lista y el precio con <see cref="ResolucionDePrecios"/> (una consulta para todas las líneas) salvo que la línea
/// ya traiga su lista fijada; sin precio → <c>Inventory.Price.NotFound</c>;</item>
/// <item>con una lista que incluye impuestos, <c>UnitPrice = ListPrice ÷ (1 + Σ tarifas porcentuales generadas)</c> a seis
/// decimales, con las tarifas que el motor tributario aplicaría a esa línea;</item>
/// <item>los descuentos manuales —precio digitado bajo la lista (<c>IsPriceOverride</c>), porcentaje, valor— contra el tope del
/// vendedor (<see cref="TopeDeDescuento"/>, el mayor de sus roles; sin fila 0); varios en la misma línea se miden juntos; el
/// descuento por total se prorratea a las líneas con el residuo por <c>Redondeo.Residuo</c>
/// (<c>FromDocumentDiscount</c>) y se compara con el tope por total. Sobre el tope no se rechaza: queda <c>RequiresApproval</c>;</item>
/// <item>I6 (T875, FR-055, F9): las promociones vigentes (<see cref="LectorDePromocionesVigentes"/>) sobre el documento entero con
/// <see cref="MotorDePromociones"/>, en base sin impuestos: descuentos no condicionados de la línea (<c>Source = Promotion</c>), que no
/// se miden contra el tope del vendedor. Un descuento manual (precio digitado bajo la lista, porcentaje o valor) en una línea con
/// promoción → <c>Inventory.Discount.PromotionApplied</c>; el descuento por total se prorratea sólo a las líneas sin promoción;</item>
/// <item>impuestos y retenciones con <see cref="MotorTributario"/> en perspectiva de venta: vendedor = la cooperativa, comprador =
/// el perfil de la persona (sus retenciones son <c>WithholdingSuffered</c>);</item>
/// <item>el residuo de la lista con impuestos (lo que dice la lista menos bruto + impuestos) se reparte por <c>Redondeo.Residuo</c>
/// al bruto de las líneas y queda visible;</item>
/// <item>bajo costo: el neto por unidad base contra <c>INV_CostStates.AverageCost</c> del ámbito (<c>Costeo.Ambito</c>);
/// <c>Ventas.BajoCosto = Bloquear</c> → <c>Inventory.Sales.BelowCost</c>, <c>Alertar</c> → <c>belowCost</c> y aviso;</item>
/// <item>el disponible (físico − reservado) de la bodega.</item>
/// </list>
/// No escribe nada: <see cref="AplicarALinea"/> pasa el resultado a la línea y a sus descuentos. (nuevo)
/// </summary>
public sealed class PrecificacionDeVenta(IApplicationDbContext db, LectorDeCatalogoTributario catalogo, ILectorDeParametros parametros)
{
    private readonly LectorDePromocionesVigentes promociones = new(db);

    public async Task<Result<VentaPrecificada>> PrecificarAsync(PedidoDePrecificacion pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var fecha = pedido.Fecha;
        var montos = Redondeo.MontosDesde(await TextoAsync(ParametrosDeInventario.RedondeoMontos, "Centavo", fecha, ct));
        var residuo = Redondeo.ResiduoDesde(await TextoAsync(ParametrosDeInventario.RedondeoResiduo, "MayorValor", fecha, ct));
        var bajoCosto = await TextoAsync(ParametrosDeInventario.VentasBajoCosto, "Alertar", fecha, ct);
        var porBodega = await TextoAsync(ParametrosDeInventario.CosteoAmbito, "Cooperativa", fecha, ct) == "Bodega";
        var tope = pedido.VendedorUserId is { } vendedor ? await TopesDeDescuento.DelUsuarioAsync(db, vendedor, fecha, ct) : TopeDelUsuario.Ninguno;

        var lineas = pedido.Lineas.OrderBy(l => l.LineNumber).ToList();
        var productoIds = lineas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Kind, p.VatSaleTreatment, p.WithholdingConceptId })
            .ToDictionaryAsync(p => p.Id, ct);
        var unidadIds = lineas.Select(l => l.UnitId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);
        var impuestos = (await db.ProductTaxes.AsNoTracking().Where(t => productoIds.Contains(t.ProductId))
                .Select(t => new { t.ProductId, t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit })
                .ToListAsync(ct))
            .ToLookup(t => t.ProductId, t => new ImpuestoDeLinea(t.TaxDefinitionId, t.TaxRateCode, t.AppliesTo, t.TaxableUnitsPerBaseUnit));

        var foto = await catalogo.FotoAsync(fecha, ct);
        if (foto.IsFailure) return Result.Failure<VentaPrecificada>(foto.Error);
        var comprador = pedido.CompradorPersonId is int personaId
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct)
            : null;

        // (1) Lista y precio de lista.
        var contexto = new ContextoDePrecio(pedido.CompradorPersonId, await ReglasDeListaDePrecios.SegmentoDeAsync(db, pedido.CompradorPersonId, ct),
            pedido.SalesChannelId, pedido.BranchId, fecha);
        var porResolver = lineas.Where(l => l.ListaFijada is null).Select(l => l.ProductId).Distinct().ToList();
        IReadOnlyList<ListaDePreciosCandidata> candidatas = [];
        if (porResolver.Count > 0) (candidatas, _) = await ResolucionDePrecios.CandidatasAsync(db, contexto, porResolver, ct);

        var avisos = new List<string>();
        var borradores = new List<Borrador>(lineas.Count);
        foreach (var l in lineas)
        {
            var producto = productos.GetValueOrDefault(l.ProductId);
            if (producto is null) return Result.Failure<VentaPrecificada>(Error.NotFound);
            PrecioFijado precio;
            var respaldo = false;
            if (l.ListaFijada is { } fijada)
            {
                precio = fijada;
            }
            else
            {
                var resuelto = ResolutorDeListaDePrecios.Resolver(candidatas, contexto, l.ProductId, l.UnitId);
                if (!resuelto.Found)
                    return Result.Failure<VentaPrecificada>(ErroresDePrecios.PriceNotFound(producto.Code, unidades.GetValueOrDefault(l.UnitId) ?? "?"));
                precio = new PrecioFijado(resuelto.PriceListId, resuelto.Price!.Value, resuelto.IncludesTaxes);
                respaldo = resuelto.FallbackUsed;
                if (respaldo) avisos.Add($"Línea {l.LineNumber}: la lista que aplica no trae {producto.Code}; el precio sale de la lista {resuelto.PriceListCode}.");
            }
            borradores.Add(new Borrador(l, producto.Code, producto.Kind, producto.VatSaleTreatment, producto.WithholdingConceptId,
                impuestos[l.ProductId].ToList(), precio, respaldo));
        }

        // (2) Precio sin impuestos: la fracción que el motor aplicaría a cada línea con lista que los incluye.
        var conImpuestos = borradores.Where(b => b.Precio.IncludesTaxes).ToList();
        if (conImpuestos.Count > 0)
        {
            var prueba = MotorTributario.Calcular(foto.Value, Entrada(fecha, pedido.MunicipioDane, foto.Value, comprador,
                conImpuestos.Select(b => b.Tributaria(Redondeo.Monto(b.Linea.Quantity * b.Precio.ListPrice, montos))).ToList()));
            foreach (var b in conImpuestos)
                b.Fraccion = prueba.Renglones.Where(r => r.Linea == b.Linea.LineNumber && !r.EsRetencion && r.Treatment == TaxTreatment.Generated && r.Rate is not null)
                    .Sum(r => r.Rate!.Value);
        }

        // (3) Bruto y descuentos manuales, en base sin impuestos; la fracción es la misma en las dos bases.
        foreach (var b in borradores)
        {
            var divisor = b.Precio.IncludesTaxes ? 1m + b.Fraccion : 1m;
            b.UnitPrice = Redondeo.CostoUnitario(b.Precio.ListPrice / divisor);
            b.Gross = Redondeo.Monto(b.Linea.Quantity * b.UnitPrice, montos);
            byte secuencia = 0;
            if (b.Linea.PrecioDigitado is { } digitado
                && TopeDeDescuento.PorPrecioDigitado(b.UnitPrice, digitado / divisor, b.Linea.Quantity, tope, montos) is { } porPrecio)
                b.Descuentos.Add(Fila(++secuencia, porPrecio, fromDocument: false, rate: null));
            if (b.Linea.DescuentoPorcentaje is { } tasa && tasa > 0m)
                b.Descuentos.Add(Fila(++secuencia, TopeDeDescuento.PorPorcentaje(b.Gross, tasa, tope, montos), fromDocument: false, rate: tasa));
            if (b.Linea.DescuentoValor is { } valor && valor > 0m)
                b.Descuentos.Add(Fila(++secuencia, TopeDeDescuento.PorValor(b.Gross, Redondeo.Monto(valor / divisor, montos), tope), fromDocument: false, rate: null));
            // Varios descuentos en la misma línea se miden juntos contra el tope por línea.
            if (b.Descuentos.Count > 1 && TopeDeDescuento.SuperaTopeDeLinea(b.Gross, b.Descuentos.Select(d => d.Amount), tope))
                for (var i = 0; i < b.Descuentos.Count; i++) b.Descuentos[i] = b.Descuentos[i] with { RequiresApproval = true };
            if (b.Descuentos.Sum(d => d.Amount) > b.Gross)
                return Result.Failure<VentaPrecificada>(new Error(Error.Validation.Code,
                    $"Los descuentos de la línea {b.Linea.LineNumber} superan su valor."));
        }

        // Promociones (I6, T875): el documento entero, sobre el bruto sin impuestos; con promoción no entra un manual (F9).
        var enPromocion = new HashSet<int>();
        var codigosDePromocion = new SortedSet<string>(StringComparer.Ordinal);
        var paraPromociones = await promociones.LeerAsync(fecha, pedido.CompradorPersonId, pedido.SalesChannelId, productoIds, montos, residuo, ct);
        if (!paraPromociones.Vacio)
        {
            var resultadoPromociones = MotorDePromociones.Aplicar(
                borradores.Where(b => b.Linea.QuantityBase > 0m && b.Gross > 0m)
                    .Select(b => paraPromociones.Linea(b.Linea.LineNumber, b.Linea.ProductId, b.Linea.QuantityBase, b.Gross / b.Linea.QuantityBase, b.Gross))
                    .ToList(),
                paraPromociones.Contexto, paraPromociones.Promociones);
            var porLineaDePromocion = resultadoPromociones.Descuentos.ToLookup(d => d.LineNumber);
            var conManual = borradores.Where(b => porLineaDePromocion[b.Linea.LineNumber].Any() && b.Descuentos.Count > 0).ToList();
            if (conManual.Count > 0)
                return Result.Failure<VentaPrecificada>(ErroresDePrecios.PromotionApplied(conManual.Select(b => b.Linea.LineNumber).ToList(),
                    conManual.SelectMany(b => porLineaDePromocion[b.Linea.LineNumber]).Select(d => d.PromotionCode).Distinct().ToList()));
            foreach (var b in borradores)
            {
                foreach (var d in porLineaDePromocion[b.Linea.LineNumber])
                {
                    b.Descuentos.Add(new DescuentoCalculado((byte)(b.Descuentos.Count + 1), false, false, d.Rate, d.Amount, tope.MaxLineRate, false,
                        DiscountSource.Promotion, d.PromotionId, d.Explicacion));
                    enPromocion.Add(b.Linea.LineNumber);
                    codigosDePromocion.Add(d.PromotionCode);
                }
            }
        }

        // Descuento por total, prorrateado a los netos.
        DescuentoPorTotal? porTotal = null;
        if (pedido.DescuentoPorTotal is { } descuentoTotal && descuentoTotal > 0m)
        {
            // Las líneas con promoción no reciben parte del descuento por total (F9: el manual no se suma a una promoción).
            var netos = borradores.Select(b => enPromocion.Contains(b.Linea.LineNumber) ? 0m : b.Gross - b.Descuentos.Sum(d => d.Amount)).ToList();
            if (enPromocion.Count > 0 && netos.Sum() <= 0m)
                return Result.Failure<VentaPrecificada>(ErroresDePrecios.PromotionApplied(enPromocion.Order().ToList(), codigosDePromocion.ToList()));
            if (descuentoTotal > netos.Sum())
                return Result.Failure<VentaPrecificada>(new Error(Error.Validation.Code, "El descuento por total supera el valor de la venta."));
            porTotal = TopeDeDescuento.PorTotal(descuentoTotal, netos, tope, montos, residuo);
            for (var i = 0; i < borradores.Count; i++)
            {
                if (porTotal.Parts[i] == 0m) continue;
                var b = borradores[i];
                b.Descuentos.Add(new DescuentoCalculado((byte)(b.Descuentos.Count + 1), true, false, porTotal.Rate, porTotal.Parts[i],
                    porTotal.CapRateApplied, porTotal.RequiresApproval));
            }
        }

        // (4) Impuestos y retenciones sobre los netos.
        var resultado = MotorTributario.Calcular(foto.Value, Entrada(fecha, pedido.MunicipioDane, foto.Value, comprador,
            borradores.Select(b => b.Tributaria(b.Net)).ToList()));
        if (resultado.Rechazado)
            return Result.Failure<VentaPrecificada>(new Error(resultado.Rechazos[0].Codigo, resultado.Rechazos[0].Mensaje));
        foreach (var b in borradores)
            b.Tax = resultado.Renglones.Where(r => r.Linea == b.Linea.LineNumber && !r.EsRetencion && r.Treatment == TaxTreatment.Generated).Sum(r => r.Amount);

        // (5) El residuo de las listas con impuestos, sólo donde no hubo descuentos (con descuentos la lista ya no es el total).
        var sinDescuento = borradores.Where(b => b.Precio.IncludesTaxes && b.Descuentos.Count == 0).ToList();
        if (sinDescuento.Count > 0)
        {
            var diferencias = sinDescuento.Select(b => Redondeo.Monto(b.Linea.Quantity * b.Precio.ListPrice, montos) - (b.Gross + b.Tax)).ToList();
            var total = diferencias.Sum();
            if (total != 0m)
            {
                var suma = sinDescuento.Sum(b => b.Gross);
                var proporcional = sinDescuento.Select(b => suma > 0m ? total * b.Gross / suma : 0m).ToList();
                var partes = Redondeo.Repartir(total, proporcional, montos, residuo);
                for (var i = 0; i < sinDescuento.Count; i++)
                {
                    sinDescuento[i].Residuo = partes[i];
                    sinDescuento[i].Gross += partes[i];
                }
            }
        }

        // (6) Bajo costo y (7) disponible.
        var inventariables = borradores.Where(b => b.Kind is ProductKind.Inventoriable or ProductKind.Variant).Select(b => b.Linea.ProductId).Distinct().ToList();
        var ambito = porBodega ? pedido.WarehouseId ?? 0 : 0;
        var costos = await db.CostStates.AsNoTracking().Where(c => inventariables.Contains(c.ProductId) && c.ScopeWarehouseId == ambito)
            .ToDictionaryAsync(c => c.ProductId, c => c.AverageCost, ct);
        var disponibles = pedido.WarehouseId is int bodega
            ? await db.StockBalances.AsNoTracking().Where(s => inventariables.Contains(s.ProductId) && s.WarehouseId == bodega)
                .ToDictionaryAsync(s => s.ProductId, s => s.Physical - s.Reserved, ct)
            : new Dictionary<int, decimal>();

        var salida = new List<LineaPrecificada>(borradores.Count);
        foreach (var b in borradores)
        {
            var inventariable = b.Kind is ProductKind.Inventoriable or ProductKind.Variant;
            decimal? promedio = inventariable && costos.TryGetValue(b.Linea.ProductId, out var c) ? c : null;
            var netoPorUnidadBase = b.Linea.QuantityBase > 0m ? b.Net / b.Linea.QuantityBase : 0m;
            var bajo = promedio is { } costo && costo > 0m && netoPorUnidadBase < costo;
            if (bajo)
            {
                if (string.Equals(bajoCosto, "Bloquear", StringComparison.OrdinalIgnoreCase))
                    return Result.Failure<VentaPrecificada>(ErroresDePrecios.BelowCost(b.Linea.LineNumber, b.Code, Redondeo.CostoUnitario(netoPorUnidadBase), promedio!.Value));
                avisos.Add($"Línea {b.Linea.LineNumber}: {b.Code} se vende a {netoPorUnidadBase:N2} por unidad y su costo promedio es {promedio:N2}.");
            }
            salida.Add(new LineaPrecificada(b.Linea.LineNumber, b.Linea.ProductId, b.Linea.UnitId, b.Linea.Quantity, b.Linea.QuantityBase,
                b.Precio.PriceListId, b.Precio.ListPrice, b.Precio.IncludesTaxes, b.Respaldo, b.UnitPrice, b.Gross, b.Descuentos.ToList(),
                b.Descuentos.Sum(d => d.Amount), b.Net, b.Tax, b.Residuo, promedio, bajo,
                inventariable && pedido.WarehouseId is not null ? disponibles.GetValueOrDefault(b.Linea.ProductId) : null));
        }

        var subtotal = salida.Sum(l => l.GrossAmount);
        var descuentos = salida.Sum(l => l.DiscountAmount);
        var impuestosTotal = salida.Sum(l => l.TaxAmount);
        var retenciones = resultado.Renglones.Where(r => r.EsRetencion).Sum(r => r.Amount);
        var totalVenta = subtotal - descuentos + impuestosTotal;
        return Result.Success(new VentaPrecificada(salida, resultado.Renglones,
            new TotalesDeVenta(subtotal, descuentos, impuestosTotal, retenciones, totalVenta, totalVenta - retenciones),
            tope, porTotal, avisos, resultado.Omisiones));
    }

    /// <summary>
    /// Pasa una línea precificada a la línea del documento y a sus descuentos (nuevos; los que tenía se dan de baja). No toca la
    /// aprobación: la pide <see cref="Pricing.AprobacionDeDescuentos"/> sobre los descuentos que la exigen.
    /// </summary>
    public static IReadOnlyList<DocumentLineDiscount> AplicarALinea(InventoryDocument documento, InventoryDocumentLine linea, LineaPrecificada precificada,
        ICollection<DocumentLineDiscount> descuentosVivos, string? motivo = null)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(precificada);
        linea.PriceListId = precificada.PriceListId;
        linea.ListPrice = precificada.ListPrice;
        linea.ListPriceIncludesTaxes = precificada.ListPriceIncludesTaxes;
        linea.UnitPrice = precificada.UnitPrice;
        linea.GrossAmount = precificada.GrossAmount;
        linea.DiscountAmount = precificada.DiscountAmount;
        linea.NetAmount = precificada.NetAmount;

        foreach (var viejo in descuentosVivos) viejo.IsDeleted = true;
        return precificada.Descuentos.Select(d => new DocumentLineDiscount
        {
            DocumentLine = linea,
            DocumentLineId = linea.Id,
            DocumentId = documento.Id,
            Sequence = d.Sequence,
            Source = d.Source,
            PromotionId = d.PromotionId,
            FromDocumentDiscount = d.FromDocumentDiscount,
            IsPriceOverride = d.IsPriceOverride,
            Rate = d.Rate,
            Amount = d.Amount,
            CapRateApplied = d.CapRateApplied,
            RequiresApproval = d.RequiresApproval,
            Reason = d.RequiresApproval ? motivo : null,
        }).ToList();
    }

    private static DescuentoCalculado Fila(byte secuencia, DescuentoDeLinea d, bool fromDocument, decimal? rate) =>
        new(secuencia, fromDocument, d.IsPriceOverride, rate ?? d.Rate, d.Amount, d.CapRateApplied, d.RequiresApproval);

    private static EntradaTributaria Entrada(DateOnly fecha, string? municipio, TaxCatalogSnapshot foto, Domain.Entities.Core.Person? comprador,
        IReadOnlyList<LineaTributaria> lineas) => new()
    {
        Fecha = fecha,
        Perspectiva = TaxAppliesTo.Sales,
        Vendedor = foto.Cooperativa,
        Comprador = CalculoTributarioDeCompra.Perfil(comprador),
        MunicipioDane = municipio,
        Lineas = lineas,
    };

    private async Task<string> TextoAsync(string clave, string defecto, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
    }

    /// <summary>Lo que se va armando de una línea.</summary>
    private sealed class Borrador(
        LineaAPrecificar linea, string code, ProductKind kind, VatSaleTreatment tratamiento, int? concepto, IReadOnlyList<ImpuestoDeLinea> impuestos,
        PrecioFijado precio, bool respaldo)
    {
        public LineaAPrecificar Linea { get; } = linea;
        public string Code { get; } = code;
        public ProductKind Kind { get; } = kind;
        public PrecioFijado Precio { get; } = precio;
        public bool Respaldo { get; } = respaldo;
        public decimal Fraccion { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Gross { get; set; }
        public decimal Tax { get; set; }
        public decimal Residuo { get; set; }
        public List<DescuentoCalculado> Descuentos { get; } = [];
        public decimal Net => Gross - Descuentos.Sum(d => d.Amount);

        public LineaTributaria Tributaria(decimal baseGravable) =>
            new(Linea.LineNumber, baseGravable, Linea.QuantityBase, tratamiento, impuestos, concepto);
    }
}
