namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// A dónde lleva una fila de un informe de inventario al profundizar (feature 012, US17, T958): la columna oculta
/// <c>_documento</c> abre la pantalla del documento según su clase (<c>DocumentClass</c>), y <c>_producto</c> con <c>_bodega</c> el
/// kardex. Una clase sin pantalla de detalle (saldo inicial, recepción de traslado, anulación, lo de I2 en adelante) devuelve nulo:
/// la pantalla lo dice en vez de navegar a «Página no encontrada». (nuevo)
/// </summary>
public static class DestinoDeDocumentoDeInventario
{
    /// <summary>La ruta del documento de clase <paramref name="clase"/>, o nula si su clase no tiene pantalla de detalle.</summary>
    public static string? Documento(int clase, Guid id) => clase switch
    {
        // I5 (T811, T812, T814): la solicitud, la orden y los costos adicionales abren su detalle de Compras.
        1 => $"/compras/solicitudes/{id}",
        2 => $"/compras/ordenes/{id}",
        8 => $"/compras/costos-adicionales/{id}",
        3 => $"/compras/recepciones/{id}",
        4 => $"/compras/facturas-proveedor/{id}",
        // I4 (T757): el documento soporte y su nota de ajuste abren su detalle de Compras.
        6 or 7 => $"/compras/documentos-soporte/{id}",
        10 or 11 or 12 or 13 => $"/inventario/ajustes/{id}",
        16 => $"/inventario/traslados/{id}",
        19 => $"/inventario/conteos/{id}",
        // I3 (T642): las ventas y sus notas de I3 abren su detalle de Ventas.
        26 or 28 or 29 or 30 or 31 or 32 => $"/ventas/documentos/{id}",
        _ => null,
    };

    /// <summary>El kardex del producto (y de la bodega, si viene), o nulo si el producto no es un PublicId.</summary>
    public static string? Kardex(string? producto, string? bodega) => Kardex(producto, bodega, null);

    /// <summary>
    /// El kardex del producto, de la bodega si viene y, desde I6 (T970), del lote si viene (<c>_lote</c> lleva su código, que es lo que filtra
    /// el kardex con <c>?lot=</c>).
    /// </summary>
    public static string? Kardex(string? producto, string? bodega, string? lote)
    {
        if (!Guid.TryParse(producto, out var p)) return null;
        var ruta = Guid.TryParse(bodega, out var b) ? $"/inventario/kardex?product={p}&warehouse={b}" : $"/inventario/kardex?product={p}";
        return string.IsNullOrWhiteSpace(lote) ? ruta : $"{ruta}&lot={Uri.EscapeDataString(lote.Trim())}";
    }
}
