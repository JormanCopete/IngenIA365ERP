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

    /// <summary>Los alcances desde I6 (T940): los de I1 y la clase ABC (<c>CountScope.AbcClass</c>, A, B o C).</summary>
    public static readonly IReadOnlyList<int> AlcancesDeConteoDesdeI6 = [0, 1, 2, 3, 4];

    /// <summary><c>CountScope.AbcClass</c>.</summary>
    public const int AlcancePorClaseAbc = 4;

    /// <summary><c>TransferDiscrepancyKind.Shortage</c>: sus salidas son devolver, dar de baja o recibir tarde.</summary>
    public const int DiferenciaFaltante = 1;

    /// <summary><c>TransferDiscrepancyResolution.WriteOffFromTransit</c> y <c>.SurplusAdjustment</c>: exigen causa.</summary>
    public const int ResolucionBajaDesdeTransito = 2;
    public const int ResolucionAjusteDeSobrante = 4;

    /// <summary>Las clases de producto que se crean en I1 (<c>Inventoriable</c>, <c>Service</c>).</summary>
    public static readonly IReadOnlyList<int> ClasesDeProductoDeI1 = [1, 2];

    /// <summary>
    /// Las clases que ofrece el alta desde I6 (T937): inventariable, servicio, combo, kit y plantilla. La variante no: nace de su plantilla
    /// (<c>Inventory.Variant.ParentRequired</c>).
    /// </summary>
    public static readonly IReadOnlyList<int> ClasesDeProductoAlCrear = [1, 2, 3, 4, 5];

    /// <summary><c>ProductKind.Combo</c>, <c>.Kit</c>, <c>.Template</c> y <c>.Variant</c> (I6).</summary>
    public const int ClaseCombo = 3;
    public const int ClaseKit = 4;
    public const int ClasePlantilla = 5;
    public const int ClaseVariante = 6;

    /// <summary>El estado de un lote (<c>LotDto.State</c>, I6, T938).</summary>
    public const string LoteVigente = "Current";
    public const string LoteProximoAVencer = "ExpiringSoon";
    public const string LoteVencido = "Expired";
    public const string LoteSinVencimiento = "NoExpiry";

    /// <summary>El estado de un lote en castellano.</summary>
    public static string EstadoDeLote(string? estado) => estado switch
    {
        LoteVigente => "Vigente",
        LoteProximoAVencer => "Próximo a vencer",
        LoteVencido => "Vencido",
        LoteSinVencimiento => "Sin vencimiento",
        _ => estado ?? string.Empty,
    };

    /// <summary><c>VatSaleTreatment.Taxed</c>: exige una tarifa de IVA.</summary>
    public const int TratamientoGravado = 1;

    /// <summary><c>ProductStatus.Blocked</c>.</summary>
    public const int EstadoBloqueado = 3;

    // ------------------------------------------------------------- integración contable (US7, I2: T532, T537) --

    /// <summary><c>DeliveryStatus</c> (0..5): el estado de una entrega en la bandeja.</summary>
    public static IReadOnlyDictionary<int, string> EstadosDeEntrega { get; } = new Dictionary<int, string>
    {
        [0] = "Pendiente",
        [1] = "En lote",
        [2] = "Procesado",
        [3] = "Rechazado",
        [4] = "No aplica",
        [5] = "Validación fallida",
    };

    /// <summary>Los nombres de <c>DeliveryStatus</c> con que viajan los filtros (la API los lee por nombre o por número).</summary>
    public static IReadOnlyDictionary<int, string> NombresDeEstadoDeEntrega { get; } = new Dictionary<int, string>
    {
        [0] = "Pending",
        [1] = "InBatch",
        [2] = "Processed",
        [3] = "Rejected",
        [4] = "NotApplicable",
        [5] = "ValidationFailed",
    };

    /// <summary><c>DeliveryMode</c> (1..4): el modo sellado en la entrega al confirmar.</summary>
    public static IReadOnlyDictionary<int, string> ModosDeEntrega { get; } = new Dictionary<int, string>
    {
        [1] = "En línea",
        [2] = "Por lotes",
        [3] = "No pasa",
        [4] = "Siempre",
    };

    /// <summary><c>BatchTrigger</c> (1..6).</summary>
    public static IReadOnlyDictionary<int, string> DisparadoresDeLote { get; } = new Dictionary<int, string>
    {
        [1] = "Programado",
        [2] = "Cierre de turno",
        [3] = "Cierre de período",
        [4] = "Manual",
        [5] = "Reproceso",
        [6] = "Envío posterior",
    };

    public static IReadOnlyDictionary<int, string> NombresDeDisparador { get; } = new Dictionary<int, string>
    {
        [1] = "Scheduled",
        [2] = "CashSessionClose",
        [3] = "PeriodClose",
        [4] = "Manual",
        [5] = "Reprocess",
        [6] = "SendNotApplicable",
    };

    /// <summary><c>BatchStatus</c> (0..4).</summary>
    public static IReadOnlyDictionary<int, string> EstadosDeLote { get; } = new Dictionary<int, string>
    {
        [0] = "Pedido",
        [1] = "En curso",
        [2] = "Completo",
        [3] = "Completo con rechazos",
        [4] = "Vacío",
    };

    public static IReadOnlyDictionary<int, string> NombresDeEstadoDeLote { get; } = new Dictionary<int, string>
    {
        [0] = "Requested",
        [1] = "Running",
        [2] = "Completed",
        [3] = "CompletedWithRejections",
        [4] = "Empty",
    };

    /// <summary><c>PrevalidationOutcome</c> (1..3): lo que dijo la validación previa al confirmar (SC-021).</summary>
    public static IReadOnlyDictionary<int, string> ValidacionesPrevias { get; } = new Dictionary<int, string>
    {
        [1] = "Contabilizable",
        [2] = "Sin respuesta",
        [3] = "No aplica",
    };

    public static IReadOnlyDictionary<int, string> NombresDeValidacionPrevia { get; } = new Dictionary<int, string>
    {
        [1] = "Postable",
        [2] = "NoResponse",
        [3] = "NotApplicable",
    };

    /// <summary><c>PostingGranularity</c> (1..2).</summary>
    public static IReadOnlyDictionary<int, string> Granularidades { get; } = new Dictionary<int, string>
    {
        [1] = "Por documento",
        [2] = "Resumido",
    };

    /// <summary><c>DeliveryAttemptOutcome</c> (1..4): cada intento de entrega en el detalle del mensaje.</summary>
    public static IReadOnlyDictionary<int, string> ResultadosDeIntento { get; } = new Dictionary<int, string>
    {
        [1] = "Procesado",
        [2] = "Ya procesado",
        [3] = "Rechazado",
        [4] = "Reintento",
    };

    /// <summary><c>ActorKind</c> (1..2): quién pidió un lote o hizo un intento.</summary>
    public static IReadOnlyDictionary<int, string> ActoresDeIntegracion { get; } = new Dictionary<int, string>
    {
        [1] = "Persona",
        [2] = "Proceso",
    };

    /// <summary><c>DeliveryStatus.Rejected</c>: lo único que se reprocesa.</summary>
    public const int EntregaRechazada = 3;

    /// <summary><c>DeliveryStatus.NotApplicable</c>: lo que se envía después (FR-078).</summary>
    public const int EntregaNoAplica = 4;

    /// <summary><c>ActorKind.Person</c>.</summary>
    public const int ActorPersona = 1;

    /// <summary><c>PostingGranularity.Summarized</c>.</summary>
    public const int GranularidadResumida = 2;

    /// <summary>Los destinos de la bandeja (<c>IntegrationDestinations</c>).</summary>
    public const string DestinoContabilidad = "Accounting";
    public const string DestinoCartera = "Lending";

    /// <summary>
    /// Los tipos de mensaje se guardan sin tildes (decisiones-transversales §2.1) y la pantalla los muestra con ellas:
    /// «DevoluciónRegistrada», «NotaCréditoEmitida», «NotaDébitoEmitida». Los demás no llevan tilde.
    /// </summary>
    public static string TipoDeMensaje(string? tipo) => tipo switch
    {
        null => "",
        "DevolucionRegistrada" => "DevoluciónRegistrada",
        "NotaCreditoEmitida" => "NotaCréditoEmitida",
        "NotaDebitoEmitida" => "NotaDébitoEmitida",
        _ => tipo,
    };

    public static string EstadoDeEntrega(int estado) => Texto(EstadosDeEntrega, estado);

    public static string Destino(string? destino) => destino switch
    {
        DestinoContabilidad => "Contabilidad",
        DestinoCartera => "Cartera",
        null => "",
        _ => destino,
    };

    /// <summary>El nombre de un valor para un filtro de la API, o nulo si no hay valor.</summary>
    public static string? Nombre(IReadOnlyDictionary<int, string> nombres, int? valor) =>
        valor is { } v && nombres.TryGetValue(v, out var n) ? n : null;

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
