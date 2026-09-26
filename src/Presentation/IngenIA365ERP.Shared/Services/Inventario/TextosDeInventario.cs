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

    /// <summary><c>ProductKind</c> (1..6); en I1 sólo se crean inventariables y servicios.</summary>
    public static IReadOnlyDictionary<int, string> ClasesDeProducto { get; } = new Dictionary<int, string>
    {
        [1] = "Inventariable",
        [2] = "Servicio",
        [3] = "Combo",
        [4] = "Kit",
        [5] = "Plantilla",
        [6] = "Variante",
    };

    /// <summary><c>ProductStatus</c> (1..3).</summary>
    public static IReadOnlyDictionary<int, string> EstadosDeProducto { get; } = new Dictionary<int, string>
    {
        [1] = "Activo",
        [2] = "Inactivo",
        [3] = "Bloqueado",
    };

    /// <summary><c>VatSaleTreatment</c> (1..3).</summary>
    public static IReadOnlyDictionary<int, string> TratamientosDeIva { get; } = new Dictionary<int, string>
    {
        [1] = "Gravado",
        [2] = "Exento",
        [3] = "Excluido",
    };

    /// <summary><c>ProductUnitUsage</c> (1..3).</summary>
    public static IReadOnlyDictionary<int, string> UsosDeUnidad { get; } = new Dictionary<int, string>
    {
        [1] = "Compra",
        [2] = "Venta",
        [3] = "Compra y venta",
    };

    /// <summary><c>WarehouseBehavior</c> (1..2).</summary>
    public static IReadOnlyDictionary<int, string> ComportamientosDeBodega { get; } = new Dictionary<int, string>
    {
        [1] = "Operativa",
        [2] = "Tránsito",
    };

    /// <summary><c>WarehouseActivationStatus</c> (0..1).</summary>
    public static IReadOnlyDictionary<int, string> ActivacionesDeBodega { get; } = new Dictionary<int, string>
    {
        [0] = "No activada",
        [1] = "Activa",
    };

    /// <summary><c>TransferDiscrepancyKind</c> (1..2). US10 (T379).</summary>
    public static IReadOnlyDictionary<int, string> TiposDeDiferencia { get; } = new Dictionary<int, string>
    {
        [1] = "Faltante",
        [2] = "Sobrante",
    };

    /// <summary><c>TransferDiscrepancyResolution</c> (1..4). US10 (T379).</summary>
    public static IReadOnlyDictionary<int, string> ResolucionesDeDiferencia { get; } = new Dictionary<int, string>
    {
        [1] = "Devolver al origen",
        [2] = "Baja desde el tránsito",
        [3] = "Recepción tardía",
        [4] = "Ajuste positivo del sobrante",
    };

    /// <summary>Los estados derivados de un traslado (texto de la API, §11). US10 (T379).</summary>
    public static IReadOnlyDictionary<string, string> EstadosDeTraslado { get; } = new Dictionary<string, string>
    {
        ["Draft"] = "Borrador",
        ["PendingApproval"] = "En aprobación",
        ["InTransit"] = "En tránsito",
        ["Received"] = "Recibido",
        ["ReceivedWithDiscrepancies"] = "Recibido con diferencias",
        ["Voided"] = "Anulado",
    };

    /// <summary>Los estados de una diferencia de traslado (texto de la API, §11). US10 (T379).</summary>
    public static IReadOnlyDictionary<string, string> EstadosDeDiferencia { get; } = new Dictionary<string, string>
    {
        ["Pending"] = "Pendiente",
        ["InApproval"] = "En aprobación",
        ["Resolved"] = "Resuelta",
    };

    /// <summary><c>CountKind</c> (1..2). US11 (T404).</summary>
    public static IReadOnlyDictionary<int, string> TiposDeConteo { get; } = new Dictionary<int, string>
    {
        [1] = "Total",
        [2] = "Cíclico",
    };

    /// <summary><c>CountScope</c> (0..4); la clase ABC llega en I6. US11 (T404).</summary>
    public static IReadOnlyDictionary<int, string> AlcancesDeConteo { get; } = new Dictionary<int, string>
    {
        [0] = "Toda la bodega",
        [1] = "Categorías",
        [2] = "Ubicaciones",
        [3] = "Selección de productos",
        [4] = "Clase ABC",
    };

    /// <summary>Los estados derivados de un conteo (texto de la API, §12). US11 (T404).</summary>
    public static IReadOnlyDictionary<string, string> EstadosDeConteo { get; } = new Dictionary<string, string>
    {
        ["Draft"] = "Borrador",
        ["Open"] = "Abierto",
        ["Closed"] = "Cerrado",
        ["AdjustmentPending"] = "Ajuste en aprobación",
        ["Adjusted"] = "Ajustado",
        ["Discarded"] = "Descartado",
        ["Voided"] = "Anulado",
    };

    /// <summary><c>CountScope</c> de I1 que se definen en pantalla (sin la clase ABC).</summary>
    public static readonly IReadOnlyList<int> AlcancesDeConteoDeI1 = [0, 1, 2, 3];

    /// <summary><c>TransferDiscrepancyKind.Shortage</c>: sus salidas son devolver, dar de baja o recibir tarde.</summary>
    public const int DiferenciaFaltante = 1;

    /// <summary><c>TransferDiscrepancyResolution.WriteOffFromTransit</c> y <c>.SurplusAdjustment</c>: exigen causa.</summary>
    public const int ResolucionBajaDesdeTransito = 2;
    public const int ResolucionAjusteDeSobrante = 4;

    /// <summary>Las clases de producto que se crean en I1 (<c>Inventoriable</c>, <c>Service</c>).</summary>
    public static readonly IReadOnlyList<int> ClasesDeProductoDeI1 = [1, 2];

    /// <summary><c>VatSaleTreatment.Taxed</c>: exige una tarifa de IVA.</summary>
    public const int TratamientoGravado = 1;

    /// <summary><c>ProductStatus.Blocked</c>.</summary>
    public const int EstadoBloqueado = 3;

    public static string Texto(IReadOnlyDictionary<int, string> textos, int valor) => textos.TryGetValue(valor, out var t) ? t : valor.ToString();

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
