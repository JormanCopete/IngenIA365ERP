namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Una línea editable de un documento del ciclo comercial (feature 012, I6, T894–T898): cotización, pedido, remisión, nota débito y la
/// factura desde pedido o remisiones. <see cref="OriginLinePublicId"/> es la línea de la que nace; se reenvía al volver a guardar para que el
/// borrador no pierda el vínculo. <see cref="Descuento"/> es el porcentaje manual (0–100); el de una promoción no se digita. (nuevo)
/// </summary>
public sealed class LineaDelCiclo
{
    public Guid? LinePublicId { get; set; }
    public Guid? OriginLinePublicId { get; set; }
    public Guid ProductPublicId { get; set; }
    public string Producto { get; set; } = "";
    public Guid UnitPublicId { get; set; }
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; } = 1m;
    public decimal? Precio { get; set; }

    /// <summary>
    /// 2026-11-12: el precio de lista que trajo la búsqueda al elegir el producto, para mostrarlo antes de que el borrador se guarde
    /// (después manda el <c>ListPrice</c> de la línea guardada). Nulo si la búsqueda no lo trajo (sin contexto o sin permiso de precios).
    /// </summary>
    public decimal? PrecioDeLista { get; set; }

    public decimal? Descuento { get; set; }

    /// <summary>La línea tiene una promoción aplicada: el servidor rechaza un descuento manual sobre ella (<c>Inventory.Discount.PromotionApplied</c>).</summary>
    public bool ConPromocion { get; set; }

    /// <summary>I6 (T938): el lote elegido (vacío = el que vence primero) y la serie leída, si el producto los controla.</summary>
    public string? Lote { get; set; }
    public string? Serie { get; set; }
    public bool ControlaLote { get; set; }
    public bool ControlaSerie { get; set; }
}

/// <summary>
/// El formulario común de los documentos del ciclo comercial (feature 012, I6, T894–T898): lee las líneas de un borrador guardado y arma el
/// <see cref="BorradorDeVentaRequest"/> que se manda a la ruta de su clase. No precifica ni calcula: el servidor precifica, aplica las
/// promociones y devuelve los totales en cada guardado. (nuevo)
/// </summary>
public static class FormularioDelCiclo
{
    /// <summary>Las líneas editables de un documento guardado, en orden. Un descuento de promoción no se relee como manual.</summary>
    public static List<LineaDelCiclo> Lineas(DocumentoDeVentaDto d) => d.Lines.OrderBy(l => l.LineNumber).Select(l => new LineaDelCiclo
    {
        LinePublicId = l.LinePublicId,
        OriginLinePublicId = l.OriginLinePublicId,
        ProductPublicId = l.Product.PublicId,
        Producto = $"{l.Product.Code} · {l.Product.Name}",
        UnitPublicId = l.Unit.PublicId,
        Unidad = l.Unit.Code,
        Cantidad = l.Quantity,
        Precio = PrecioDigitado(l),
        Descuento = l.Discounts.FirstOrDefault(x => !x.FromDocumentDiscount && !x.IsPriceOverride && !x.EsPromocion)?.Percent is { } p ? p * 100m : null,
        ConPromocion = l.Discounts.Any(x => x.EsPromocion),
    }).ToList();

    /// <summary>
    /// El precio que la persona escribió, en la base de la lista (con impuestos si la lista los incluye), o nulo si vende al de la lista.
    /// Escribir un precio menor es un descuento <c>IsPriceOverride</c>, y sólo él lo dice: <c>UnitPrice</c> va siempre sin impuestos, así
    /// que con una lista que los incluye nunca es igual a <c>ListPrice</c>. Hasta el 2026-11-09 se comparaban los dos, cada borrador
    /// releído mandaba el precio sin IVA como si se hubiera digitado, el servidor le volvía a quitar el IVA y toda línea quedaba con un
    /// descuento de 1 − 1/(1 + tarifa) que pedía aprobación (<c>Inventory.Discount.ApprovalPending</c>) sin que nadie hubiera descontado.
    /// </summary>
    public static decimal? PrecioDigitado(LineaDeVentaDto l) =>
        l.Discounts.FirstOrDefault(x => x.IsPriceOverride && !x.FromDocumentDiscount) is { Percent: { } fraccion }
            ? Math.Round(l.ListPrice * (1m - fraccion), 2)
            : null;

    /// <summary>Las líneas del cuerpo: con su número, su origen y el descuento manual sólo si la línea no tiene promoción.</summary>
    public static IReadOnlyList<LineaDeVentaRequest> Pedido(IEnumerable<LineaDelCiclo> lineas) => lineas.Select((l, i) => new LineaDeVentaRequest(
        l.ProductPublicId, l.UnitPublicId, l.Cantidad, l.Precio,
        l.Descuento is > 0 && !l.ConPromocion ? new DescuentoRequest(Percent: l.Descuento / 100m) : null,
        LinePublicId: l.LinePublicId, LineNumber: i + 1, OriginLinePublicId: l.OriginLinePublicId, LotCode: l.Lote, SerialNumber: l.Serie)).ToList();

    /// <summary>
    /// I6 (T938): el documento de venta no devuelve el lote ni la serie de sus líneas; al releerlo se conservan los elegidos en pantalla, por
    /// línea (<c>LinePublicId</c>, o la posición en una línea nueva).
    /// </summary>
    public static void ConservarSeguimiento(IReadOnlyList<LineaDelCiclo> antes, IReadOnlyList<LineaDelCiclo> despues)
    {
        for (var i = 0; i < despues.Count; i++)
        {
            var d = despues[i];
            var a = antes.FirstOrDefault(x => x.LinePublicId is not null && x.LinePublicId == d.LinePublicId)
                ?? (i < antes.Count && antes[i].ProductPublicId == d.ProductPublicId ? antes[i] : null);
            if (a is null) continue;
            (d.Lote, d.Serie, d.ControlaLote, d.ControlaSerie) = (a.Lote, a.Serie, a.ControlaLote, a.ControlaSerie);
            // El precio releído sale de una fracción a seis decimales; el que se escribió es el exacto (si cambiara un centavo, la
            // huella del descuento cambiaría y la aprobación pendiente se volvería a pedir).
            if (d.Precio is not null && a.Precio is not null) d.Precio = a.Precio;
        }
    }

    /// <summary>
    /// 2026-11-12: el precio de lista del producto elegido, como lo trajo la búsqueda con el contexto de precio: el del empaque leído si
    /// la lista lo trae, o el de la unidad base. Nulo si la búsqueda no trajo precio.
    /// </summary>
    public static decimal? PrecioDeLista(Inventario.ProductoElegido elegido) =>
        elegido.Empaque is { } empaque
            ? elegido.Producto.Units?.FirstOrDefault(u => u.ProductUnitPublicId == empaque.ProductUnitPublicId)?.Price
            : elegido.Producto.Price;

    /// <summary>
    /// 2026-11-12: el guardado automático terminó mientras la persona seguía cambiando líneas. La pantalla conserva sus líneas (releer
    /// las del servidor borraría lo que se agregó o se cambió mientras tanto) y sólo toma de la respuesta los identificadores de las
    /// líneas que se mandaron nuevas: <paramref name="enviadas"/> son las que viajaron, en el orden en que viajaron (su
    /// <c>LineNumber</c> es la posición + 1), y siguen siendo los mismos objetos de la pantalla si no se quitaron. Sin esto, el guardado
    /// siguiente las volvería a mandar sin identificador y el servidor las duplicaría.
    /// </summary>
    public static void TomarIdentificadores(IReadOnlyList<LineaDelCiclo> enviadas, DocumentoDeVentaDto d)
    {
        var porNumero = d.Lines.GroupBy(l => l.LineNumber).ToDictionary(g => g.Key, g => g.First());
        for (var i = 0; i < enviadas.Count; i++)
        {
            var l = enviadas[i];
            if (l.LinePublicId is not null || !porNumero.TryGetValue(i + 1, out var guardada)) continue;
            if (guardada.Product.PublicId == l.ProductPublicId) l.LinePublicId = guardada.LinePublicId;
        }
    }

    /// <summary>Lo que dice la línea sobre sus promociones: «3x2 · 2.000» por cada una (FR-055: el documento muestra cuál se aplicó).</summary>
    public static string Promociones(LineaDeVentaDto? linea) =>
        linea is null ? "" : string.Join("; ", linea.Discounts.Where(d => d.EsPromocion).Select(d => $"{d.PromotionName ?? "Promoción"} · {d.Amount:N0}"));

    /// <summary>Los días desde la fecha del documento hasta <paramref name="hoy"/> (una remisión sin facturar, FR-052).</summary>
    public static int DiasDesde(DateOnly fecha, DateOnly hoy) => Math.Max(0, hoy.DayNumber - fecha.DayNumber);

    /// <summary>
    /// Las remisiones elegidas se facturan juntas sólo si son del mismo cliente (<c>Inventory.Shipment.CustomerMismatch</c>): devuelve el
    /// motivo por el que no, o nulo.
    /// </summary>
    public static string? NoSePuedenFacturarJuntas(IReadOnlyCollection<ResumenDeVentaDto> remisiones)
    {
        if (remisiones.Count == 0) return "Elija al menos una remisión.";
        if (remisiones.Any(r => r.Status != 2)) return "Sólo se facturan remisiones confirmadas.";
        return remisiones.Select(r => r.CounterpartyPersonPublicId).Distinct().Count() > 1 ? "Las remisiones elegidas son de clientes distintos." : null;
    }
}
