namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Las etiquetas en español de los enums de Inventario que la API entrega como número (feature 012, T183, T185;
/// decisiones-transversales §2.5: «valores en inglés; la pantalla traduce»). Los números son los de
/// <c>Domain.Enums.Inventory</c> y <c>Domain.Inventory.Documents</c>; lo fija <c>TextosDeInventarioTests</c>. (nuevo)
/// </summary>
public static class TextosDeInventario
{
    /// <summary><c>DocumentClass</c> (1..34).</summary>
    public static IReadOnlyDictionary<int, string> Clases { get; } = new Dictionary<int, string>
    {
        [1] = "Solicitud de compra",
        [2] = "Orden de compra",
        [3] = "Recepción",
        [4] = "Factura del proveedor",
        [5] = "Nota del proveedor",
        [6] = "Documento soporte",
        [7] = "Nota de ajuste al documento soporte",
        [8] = "Costos adicionales",
        [9] = "Devolución a proveedor",
        [10] = "Ajuste positivo",
        [11] = "Ajuste negativo",
        [12] = "Consumo interno",
        [13] = "Baja",
        [14] = "Ensamble",
        [15] = "Saldo inicial",
        [16] = "Despacho de traslado",
        [17] = "Recepción de traslado",
        [18] = "Movimiento entre ubicaciones",
        [19] = "Conteo físico",
        [20] = "Ajuste de costo",
        [21] = "Movimiento de caja",
        [22] = "Diferencia de arqueo",
        [23] = "Cotización",
        [24] = "Pedido",
        [25] = "Remisión",
        [26] = "Factura de venta",
        [27] = "Factura desde remisiones",
        [28] = "Documento equivalente POS",
        [29] = "Comprobante de venta no electrónico",
        [30] = "Nota al comprobante no electrónico",
        [31] = "Nota crédito",
        [32] = "Nota de ajuste POS",
        [33] = "Nota débito",
        [34] = "Anulación",
    };

    /// <summary><c>DocumentClassGroup</c> (1..8).</summary>
    public static IReadOnlyDictionary<int, string> Grupos { get; } = new Dictionary<int, string>
    {
        [1] = "Compras",
        [2] = "Ajustes",
        [3] = "Traslados",
        [4] = "Conteos",
        [5] = "Ventas",
        [6] = "Caja",
        [7] = "Saldo inicial",
        [8] = "Costeo",
    };

    /// <summary><c>DocumentStatus</c> (0..4).</summary>
    public static IReadOnlyDictionary<int, string> Estados { get; } = new Dictionary<int, string>
    {
        [0] = "Borrador",
        [1] = "En aprobación",
        [2] = "Confirmado",
        [3] = "Anulado",
        [4] = "Descartado",
    };

    /// <summary><c>PostingMode</c> (1..3).</summary>
    public static IReadOnlyDictionary<int, string> ModosDePaso { get; } = new Dictionary<int, string>
    {
        [1] = "En línea",
        [2] = "Por lotes",
        [3] = "No pasa a contabilidad",
    };

    /// <summary><c>NumberedBy</c> (1..2).</summary>
    public static IReadOnlyDictionary<int, string> Numeracion { get; } = new Dictionary<int, string>
    {
        [1] = "Consecutivo propio",
        [2] = "Resolución DIAN",
    };

    /// <summary>El grupo de Compras (<c>DocumentClassGroup.Purchases</c>): sólo sus clases admiten «IVA no descontable».</summary>
    public const int GrupoDeCompras = 1;

    /// <summary><c>DocumentClass.InternalConsumption</c>: la única que admite «retiro gravado».</summary>
    public const int ClaseDeConsumoInterno = 12;

    public static string Clase(int clase) => Clases.TryGetValue(clase, out var t) ? t : $"Clase {clase}";

    public static string Grupo(int? grupo) => grupo is { } g && Grupos.TryGetValue(g, out var t) ? t : "Sin grupo";

    public static string Estado(int estado) => Estados.TryGetValue(estado, out var t) ? t : $"Estado {estado}";

    public static string ModoDePaso(int? modo) => modo is { } m && ModosDePaso.TryGetValue(m, out var t) ? t : "—";

    public static string Numerado(int numeradoPor) => Numeracion.TryGetValue(numeradoPor, out var t) ? t : "—";
}
