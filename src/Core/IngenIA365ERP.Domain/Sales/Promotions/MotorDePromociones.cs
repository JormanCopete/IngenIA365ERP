using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Sales.Promotions;

/// <summary>
/// Una línea de venta que el motor mira (nuevo): el producto (y su plantilla si es variante: un ámbito sobre la plantilla alcanza a
/// sus variantes), la ruta de categorías del producto (la suya y sus ancestros, así un ámbito sobre una categoría alcanza a sus
/// descendientes), la cantidad en <b>unidad base</b> y el precio por unidad base <b>sin impuestos</b>. <see cref="BrutoDado"/> es el
/// bruto que la precificación ya redondeó; sin él, cantidad × precio al redondeo vigente.
/// </summary>
public sealed record LineaDePromocion(
    int LineNumber,
    int ProductId,
    int? ParentProductId,
    IReadOnlyCollection<int> Categorias,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? BrutoDado = null)
{
    public decimal Bruto(RedondeoDeMontos montos) => BrutoDado ?? Redondeo.Monto(Cantidad * PrecioUnitario, montos);
}

/// <summary>El contexto del documento (nuevo): la fecha de operación, el canal, el segmento del cliente y el redondeo vigente.</summary>
public sealed record ContextoDePromocion(DateOnly Fecha, int? SalesChannelId, string? Segmento, RedondeoDeMontos Montos, ResiduoDeRedondeo Residuo);

/// <summary>
/// Un ámbito de una promoción (<c>INV_PromotionScopes</c>, nuevo): una sola columna destino según <see cref="Kind"/>.
/// <see cref="RequiredQuantity"/> sólo en <see cref="PromotionKind.BundlePrice"/>, en unidad base.
/// </summary>
public sealed record AmbitoDePromocion(
    PromotionScopeKind Kind,
    int? ProductId = null,
    int? ProductCategoryId = null,
    string? Segment = null,
    int? SalesChannelId = null,
    decimal? RequiredQuantity = null);

/// <summary>Un escalón del precio por cantidad (<c>INV_PromotionTiers</c>, nuevo): desde esa cantidad en unidad base, ese precio sin impuestos.</summary>
public sealed record TramoDePromocion(decimal MinQuantity, decimal UnitPrice);

/// <summary>Una promoción con sus ámbitos y tramos, como la entrega <c>LectorDePromocionesVigentes</c> (nuevo).</summary>
public sealed record PromocionVigente(
    int Id,
    string Code,
    string Name,
    PromotionKind Kind,
    decimal? Rate,
    decimal? Amount,
    decimal? BuyQuantity,
    decimal? PayQuantity,
    decimal? BundlePrice,
    bool IsCumulative,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    bool IsActive,
    IReadOnlyList<AmbitoDePromocion> Ambitos,
    IReadOnlyList<TramoDePromocion> Tramos);

/// <summary>
/// Un descuento no condicionado de una línea producido por una promoción (nuevo; irá a <c>INV_DocumentLineDiscounts</c> con
/// <c>Source = Promotion</c> y <c>PromotionId</c>). <see cref="Rate"/> sólo en <see cref="PromotionKind.Percent"/>. <see cref="Residuo"/>
/// es lo que el reparto le sumó (o restó) a esta línea por <c>Redondeo.Residuo</c>, visible (FR-017).
/// </summary>
public sealed record DescuentoDePromocion(int LineNumber, int PromotionId, string PromotionCode, decimal Amount, decimal? Rate, decimal Residuo, string Explicacion);

/// <summary>Una promoción no acumulable que habría descontado algo pero perdió con otra de mayor descuento (nuevo).</summary>
public sealed record PromocionDescartada(int PromotionId, string PromotionCode, decimal Amount, int GanadoraId, string Motivo);

/// <summary>Lo que devuelve el motor (nuevo): los descuentos por línea (en orden de línea) y las no acumulables que perdieron.</summary>
public sealed record ResultadoDePromociones(IReadOnlyList<DescuentoDePromocion> Descuentos, IReadOnlyList<PromocionDescartada> Descartadas)
{
    public static ResultadoDePromociones Vacio { get; } = new([], []);
}

/// <summary>
/// El motor de promociones (feature 012, I6, T872; FR-055, US14-4, F9; data-model §14 «Promociones»; decisiones-transversales §2.16,
/// T51). Puro: sin IO ni valores legales. Recibe las líneas del documento entero, el contexto y las promociones; devuelve
/// <b>descuentos no condicionados por línea</b>, nunca líneas a precio cero: lo que una promoción regala se reparte entre las líneas
/// que la ganaron, en proporción a lo que aportan, con el residuo por <c>Redondeo.Residuo</c>.
/// <list type="bullet">
/// <item><b>Aplica</b> si está activa y vigente a la fecha (los dos extremos incluidos) y cumple sus ámbitos: dentro de una misma clase
/// cualquiera (O), entre clases todas (Y), y una clase sin filas no restringe. Canal y segmento son del documento; producto (o su
/// plantilla) y categoría (o una ancestra), de cada línea.</item>
/// <item><see cref="PromotionKind.Percent"/>: la tasa sobre el bruto de cada línea. <see cref="PromotionKind.Amount"/>: el valor por
/// unidad base vendida, hasta el bruto. <see cref="PromotionKind.BuyNPayM"/>: por producto, cada juego completo de
/// <c>BuyQuantity</c> unidades regala <c>BuyQuantity − PayQuantity</c> al precio más bajo del producto en el documento.
/// <see cref="PromotionKind.QuantityPrice"/>: por producto, la cantidad del documento elige el tramo de mayor <c>MinQuantity</c>
/// alcanzado y cada línea baja de su precio al del tramo. <see cref="PromotionKind.BundlePrice"/>: los ámbitos de producto con
/// <c>RequiredQuantity</c> forman el paquete; caben tantos paquetes como el producto más escaso permita y cada uno descuenta la
/// diferencia entre sus unidades sueltas y <c>BundlePrice</c>.</item>
/// <item><b>Conflictos</b> (F9): entre no acumulables gana la de mayor descuento sobre el documento (en empate, la de menor código); una
/// no acumulable que comparte alguna línea con una que ya ganó queda descartada, y la que no se cruza con ninguna también aplica.
/// Las acumulables se suman. Lo que una línea recibe nunca supera su bruto (se recorta la última en aplicarse).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class MotorDePromociones
{
    public static ResultadoDePromociones Aplicar(IReadOnlyList<LineaDePromocion> lineas, ContextoDePromocion contexto, IReadOnlyList<PromocionVigente> promociones)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(promociones);
        if (lineas.Count == 0 || promociones.Count == 0) return ResultadoDePromociones.Vacio;

        var brutos = lineas.ToDictionary(l => l.LineNumber, l => l.Bruto(contexto.Montos));
        var calculadas = promociones
            .Where(p => Aplica(p, contexto))
            .Select(p => (Promocion: p, Partes: Calcular(p, lineas.Where(l => brutos[l.LineNumber] > 0m && AlcanzaLinea(p, l)).ToList(), contexto)))
            .Where(x => x.Partes.Count > 0)
            .ToList();
        if (calculadas.Count == 0) return ResultadoDePromociones.Vacio;

        // No acumulables: la de mayor descuento primero; una que se cruza con otra ya ganadora queda descartada.
        var ganadoras = new List<(PromocionVigente Promocion, IReadOnlyList<DescuentoDePromocion> Partes)>();
        var descartadas = new List<PromocionDescartada>();
        var tomadas = new Dictionary<int, PromocionVigente>();
        foreach (var (p, partes) in calculadas.Where(x => !x.Promocion.IsCumulative)
                     .OrderByDescending(x => x.Partes.Sum(d => d.Amount)).ThenBy(x => x.Promocion.Code, StringComparer.Ordinal).ThenBy(x => x.Promocion.Id))
        {
            var cruce = partes.Select(d => d.LineNumber).FirstOrDefault(tomadas.ContainsKey);
            if (cruce != 0 && tomadas.TryGetValue(cruce, out var ganadora))
            {
                descartadas.Add(new PromocionDescartada(p.Id, p.Code, partes.Sum(d => d.Amount), ganadora.Id,
                    $"No es acumulable y en la línea {cruce} ganó «{ganadora.Code}», de mayor descuento."));
                continue;
            }
            ganadoras.Add((p, partes));
            foreach (var d in partes) tomadas[d.LineNumber] = p;
        }
        ganadoras.AddRange(calculadas.Where(x => x.Promocion.IsCumulative).OrderBy(x => x.Promocion.Code, StringComparer.Ordinal).ThenBy(x => x.Promocion.Id));

        // Ninguna línea recibe más que su bruto.
        var usado = lineas.ToDictionary(l => l.LineNumber, _ => 0m);
        var salida = new List<(int Orden, DescuentoDePromocion Descuento)>();
        var orden = 0;
        foreach (var (_, partes) in ganadoras)
        {
            foreach (var d in partes)
            {
                var libre = brutos[d.LineNumber] - usado[d.LineNumber];
                var valor = Math.Min(d.Amount, libre);
                if (valor <= 0m) continue;
                usado[d.LineNumber] += valor;
                salida.Add((orden, valor == d.Amount ? d : d with
                {
                    Amount = valor,
                    Explicacion = d.Explicacion + $" Recortado a {valor:N2}: la línea no puede quedar por debajo de cero.",
                }));
            }
            orden++;
        }

        return new ResultadoDePromociones(
            salida.OrderBy(x => x.Descuento.LineNumber).ThenBy(x => x.Orden).Select(x => x.Descuento).ToList(),
            descartadas);
    }

    /// <summary>¿Está activa, vigente a la fecha y cumple los ámbitos del documento (canal y segmento)?</summary>
    public static bool Aplica(PromocionVigente p, ContextoDePromocion contexto)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(contexto);
        if (!p.IsActive || contexto.Fecha < p.ValidFrom || contexto.Fecha > p.ValidTo) return false;

        var canales = p.Ambitos.Where(a => a.Kind == PromotionScopeKind.Channel).ToList();
        if (canales.Count > 0 && !canales.Any(a => a.SalesChannelId is { } c && c == contexto.SalesChannelId)) return false;

        var segmentos = p.Ambitos.Where(a => a.Kind == PromotionScopeKind.Segment).ToList();
        var segmento = Normalizar(contexto.Segmento);
        return segmentos.Count == 0 || (segmento is not null && segmentos.Any(a => Normalizar(a.Segment) == segmento));
    }

    /// <summary>¿La línea cumple los ámbitos de producto y categoría?</summary>
    public static bool AlcanzaLinea(PromocionVigente p, LineaDePromocion l)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(l);
        var productos = p.Ambitos.Where(a => a.Kind == PromotionScopeKind.Product).ToList();
        if (productos.Count > 0 && !productos.Any(a => a.ProductId is { } id && (id == l.ProductId || id == l.ParentProductId))) return false;

        var categorias = p.Ambitos.Where(a => a.Kind == PromotionScopeKind.Category).ToList();
        return categorias.Count == 0 || categorias.Any(a => a.ProductCategoryId is { } c && l.Categorias.Contains(c));
    }

    private static string? Normalizar(string? segmento) =>
        string.IsNullOrWhiteSpace(segmento) ? null : segmento.Trim().ToUpperInvariant();

    /// <summary>Los descuentos de una promoción sobre las líneas que alcanza, antes de resolver conflictos.</summary>
    private static IReadOnlyList<DescuentoDePromocion> Calcular(PromocionVigente p, IReadOnlyList<LineaDePromocion> lineas, ContextoDePromocion ctx)
    {
        if (lineas.Count == 0) return [];
        return p.Kind switch
        {
            PromotionKind.Percent => PorLinea(p, lineas, ctx, l => p.Rate is { } tasa && tasa > 0m ? l.Bruto(ctx.Montos) * tasa : 0m,
                l => $"«{p.Code}»: {p.Rate:P2} sobre el bruto de {l.Bruto(ctx.Montos):N2}.", p.Rate),
            PromotionKind.Amount => PorLinea(p, lineas, ctx,
                l => p.Amount is { } valor && valor > 0m ? Math.Min(valor * l.Cantidad, l.Bruto(ctx.Montos)) : 0m,
                l => $"«{p.Code}»: {p.Amount:N2} por unidad × {l.Cantidad:0.####}.", null),
            PromotionKind.BuyNPayM => LleveYPague(p, lineas, ctx),
            PromotionKind.QuantityPrice => PrecioPorCantidad(p, lineas, ctx),
            PromotionKind.BundlePrice => Paquete(p, lineas, ctx),
            _ => [],
        };
    }

    private static IReadOnlyList<DescuentoDePromocion> PorLinea(PromocionVigente p, IReadOnlyList<LineaDePromocion> lineas, ContextoDePromocion ctx,
        Func<LineaDePromocion, decimal> valor, Func<LineaDePromocion, string> explicacion, decimal? tasa)
    {
        var sinRedondear = lineas.Select(valor).ToList();
        return Repartir(p, lineas, sinRedondear, Redondeo.Monto(sinRedondear.Sum(), ctx.Montos), ctx, i => explicacion(lineas[i]), tasa);
    }

    /// <summary>«Lleve N pague M» por producto: cada juego completo regala N − M unidades al precio más bajo del producto.</summary>
    private static IReadOnlyList<DescuentoDePromocion> LleveYPague(PromocionVigente p, IReadOnlyList<LineaDePromocion> lineas, ContextoDePromocion ctx)
    {
        if (p.BuyQuantity is not { } lleva || p.PayQuantity is not { } paga || lleva <= 0m || paga < 0m || paga >= lleva) return [];
        var salida = new List<DescuentoDePromocion>();
        foreach (var grupo in lineas.GroupBy(l => l.ProductId))
        {
            var delProducto = grupo.ToList();
            var juegos = decimal.Floor(delProducto.Sum(l => l.Cantidad) / lleva);
            if (juegos <= 0m) continue;
            var regaladas = juegos * (lleva - paga);
            var precio = delProducto.Min(l => l.PrecioUnitario);
            var total = Redondeo.Monto(regaladas * precio, ctx.Montos);
            var brutos = delProducto.Select(l => l.Bruto(ctx.Montos)).ToList();
            var suma = brutos.Sum();
            if (total <= 0m || suma <= 0m) continue;
            salida.AddRange(Repartir(p, delProducto, brutos.Select(b => total * b / suma).ToList(), total, ctx,
                _ => $"«{p.Code}» lleve {lleva:0.####} pague {paga:0.####}: {juegos:0} juego(s), {regaladas:0.####} unidad(es) sin cobro a "
                     + $"{precio:N2} = {total:N2}, repartido entre {delProducto.Count} línea(s).", null));
        }
        return salida;
    }

    /// <summary>Precio por cantidad: la cantidad del producto en el documento elige el tramo; cada línea baja al precio del tramo.</summary>
    private static IReadOnlyList<DescuentoDePromocion> PrecioPorCantidad(PromocionVigente p, IReadOnlyList<LineaDePromocion> lineas, ContextoDePromocion ctx)
    {
        if (p.Tramos.Count == 0) return [];
        var salida = new List<DescuentoDePromocion>();
        foreach (var grupo in lineas.GroupBy(l => l.ProductId))
        {
            var delProducto = grupo.ToList();
            var cantidad = delProducto.Sum(l => l.Cantidad);
            var tramo = p.Tramos.Where(t => t.MinQuantity <= cantidad).OrderByDescending(t => t.MinQuantity).FirstOrDefault();
            if (tramo is null) continue;
            var sinRedondear = delProducto.Select(l => l.PrecioUnitario > tramo.UnitPrice
                ? Math.Min((l.PrecioUnitario - tramo.UnitPrice) * l.Cantidad, l.Bruto(ctx.Montos))
                : 0m).ToList();
            var total = Redondeo.Monto(sinRedondear.Sum(), ctx.Montos);
            if (total <= 0m) continue;
            salida.AddRange(Repartir(p, delProducto, sinRedondear, total, ctx,
                i => $"«{p.Code}»: {cantidad:0.####} unidades alcanzan el tramo desde {tramo.MinQuantity:0.####} a {tramo.UnitPrice:N2}; "
                     + $"la línea baja de {delProducto[i].PrecioUnitario:N2}.", null));
        }
        return salida;
    }

    /// <summary>Precio de paquete: tantos paquetes como permita el producto más escaso; cada uno descuenta sus sueltas menos el precio.</summary>
    private static IReadOnlyList<DescuentoDePromocion> Paquete(PromocionVigente p, IReadOnlyList<LineaDePromocion> lineas, ContextoDePromocion ctx)
    {
        if (p.BundlePrice is not { } precioPaquete) return [];
        var componentes = p.Ambitos.Where(a => a.Kind == PromotionScopeKind.Product && a.ProductId is not null && a.RequiredQuantity is > 0m).ToList();
        if (componentes.Count == 0) return [];

        var porProducto = componentes.ToDictionary(c => c.ProductId!.Value,
            c => lineas.Where(l => l.ProductId == c.ProductId || l.ParentProductId == c.ProductId).ToList());
        var paquetes = componentes.Min(c => decimal.Floor(porProducto[c.ProductId!.Value].Sum(l => l.Cantidad) / c.RequiredQuantity!.Value));
        if (paquetes <= 0m) return [];

        // Cada línea aporta al paquete, en su orden, hasta completar las unidades del componente; su peso es lo aportado a su precio.
        var incluidas = new List<LineaDePromocion>();
        var pesos = new List<decimal>();
        foreach (var c in componentes)
        {
            var faltan = paquetes * c.RequiredQuantity!.Value;
            foreach (var l in porProducto[c.ProductId!.Value].OrderBy(l => l.LineNumber))
            {
                if (faltan <= 0m) break;
                var aporta = Math.Min(faltan, l.Cantidad);
                faltan -= aporta;
                incluidas.Add(l);
                pesos.Add(aporta * l.PrecioUnitario);
            }
        }
        var sueltas = pesos.Sum();
        var total = Redondeo.Monto(sueltas - paquetes * precioPaquete, ctx.Montos);
        if (total <= 0m || sueltas <= 0m) return [];

        var orden = incluidas.Select((l, i) => (l, i)).OrderBy(x => x.l.LineNumber).ToList();
        return Repartir(p, orden.Select(x => x.l).ToList(), orden.Select(x => total * pesos[x.i] / sueltas).ToList(), total, ctx,
            _ => $"«{p.Code}»: {paquetes:0} paquete(s) a {precioPaquete:N2}; sueltas valen {sueltas:N2}, descuento {total:N2} repartido por lo que "
                 + "cada línea aporta.", null);
    }

    /// <summary>Reparte <paramref name="total"/> entre las líneas por <c>Redondeo.Residuo</c> y deja el residuo visible en cada parte.</summary>
    private static IReadOnlyList<DescuentoDePromocion> Repartir(PromocionVigente p, IReadOnlyList<LineaDePromocion> lineas, IReadOnlyList<decimal> sinRedondear,
        decimal total, ContextoDePromocion ctx, Func<int, string> explicacion, decimal? tasa)
    {
        if (total <= 0m) return [];
        var partes = Redondeo.Repartir(total, sinRedondear, ctx.Montos, ctx.Residuo);
        var salida = new List<DescuentoDePromocion>(lineas.Count);
        for (var i = 0; i < lineas.Count; i++)
        {
            if (partes[i] <= 0m) continue;
            var residuo = partes[i] - Redondeo.Monto(sinRedondear[i], ctx.Montos);
            var texto = explicacion(i) + (residuo != 0m ? $" Residuo de redondeo asignado a esta línea: {residuo:N2}." : string.Empty);
            salida.Add(new DescuentoDePromocion(lineas[i].LineNumber, p.Id, p.Code, partes[i], tasa, residuo, texto));
        }
        return salida;
    }
}
