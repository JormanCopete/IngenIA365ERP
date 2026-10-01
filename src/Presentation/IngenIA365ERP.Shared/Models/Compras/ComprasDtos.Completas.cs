using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Models.Compras;

// Los DTO espejo de compras completas (feature 012, I5, T810; contracts/api.md §14.8, §14.9): solicitudes y órdenes con lo pendiente por
// línea, el cruce a tres vías, los costos adicionales con su reparto y la emisión RADIAN desde el ERP. Los enums llegan como número y se
// mandan por nombre o número (EnumPorNombreONumero); sólo PublicId (Principio VI). (nuevos)

/// <summary>
/// Lo propio de una solicitud o una orden en su detalle (<c>PurchasePlanInfoDto</c>): para cuándo se necesita, la entrega esperada, las
/// condiciones de pago de la orden y el cierre de su saldo.
/// </summary>
public sealed record PlanDeCompraDto(
    DateOnly? NeededBy,
    DateOnly? ExpectedDate,
    string? PaymentTerms,
    DateTime? BalanceClosedAt,
    UsuarioDeInventarioDto? BalanceClosedBy,
    string? BalanceClosedReason);

/// <summary>
/// Por línea de una solicitud (<see cref="PendingToOrder"/>) o de una orden (<see cref="PendingToReceive"/>), en unidad base: lo pedido, lo
/// consumido por sus vínculos y lo que queda (<c>PurchasePendingLineDto</c>). Se calcula en el servidor; nunca se guarda.
/// </summary>
public sealed record PendienteDeLineaDeCompraDto(Guid LinePublicId, int LineNumber, decimal Quantity, decimal Consumed, decimal? PendingToOrder, decimal? PendingToReceive);

/// <summary>La factura de una línea del cruce: su número en la cooperativa y el del proveedor.</summary>
public sealed record FacturaDelCruceDto(Guid PublicId, string? DisplayNumber, string? SupplierNumber);

/// <summary>
/// <c>PurchaseMatchLineDto</c> (§14.9): una línea de la factura cruzada contra lo ordenado y lo recibido, en unidad base. Precios y
/// diferencia de precio nulos sin <c>Inventory.Costs.Read</c>. <see cref="Status"/> (<c>PurchaseMatchStatus</c>: 1 retenida, 2 aprobada,
/// 3 rechazada) nulo = dentro de la tolerancia. <see cref="Tolerance"/> es el JSON de la tolerancia usada.
/// </summary>
public sealed record LineaDelCruceDto(
    Guid PublicId,
    FacturaDelCruceDto SupplierInvoice,
    int LineNumber,
    ReferenciaDeInventarioDto Product,
    decimal Ordered,
    decimal Received,
    decimal Invoiced,
    decimal? OrderPrice,
    decimal? ReceiptPrice,
    decimal? InvoicePrice,
    decimal QuantityVariance,
    decimal? PriceVariance,
    IReadOnlyList<string> Reasons,
    int? Status,
    Guid? ApprovalRequestPublicId,
    bool ExceedsTolerance,
    string Tolerance);

/// <summary>Los estados del cruce (<c>PurchaseMatchStatus</c>) y sus razones (<c>Quantity</c>, <c>Price</c>) en palabras.</summary>
public static class EstadosDelCruce
{
    public const int Retenida = 1;
    public const int Aprobada = 2;
    public const int Rechazada = 3;

    public static readonly IReadOnlyList<(int Valor, string Texto)> Todos = [(Retenida, "Retenida"), (Aprobada, "Aprobada"), (Rechazada, "Rechazada")];

    public static string Texto(int? estado) => estado switch
    {
        Retenida => "Retenida",
        Aprobada => "Aprobada",
        Rechazada => "Rechazada",
        null => "Dentro de la tolerancia",
        _ => estado.Value.ToString(),
    };

    public static string Razon(string razon) => razon switch
    {
        "Quantity" => "Cantidad",
        "Price" => "Precio",
        _ => razon,
    };
}

/// <summary>Los métodos de reparto de los costos adicionales (<c>LandedCostAllocationMethod</c>), por nombre y por número.</summary>
public static class MetodosDeReparto
{
    public const string Valor = "Value";
    public const string Cantidad = "Quantity";
    public const string Peso = "Weight";
    public const string Volumen = "Volume";
    public const string Manual = "Manual";

    public static readonly IReadOnlyList<(string Valor, int Numero, string Texto)> Todos =
    [
        (Valor, 1, "Por valor"), (Cantidad, 2, "Por cantidad"), (Peso, 3, "Por peso"), (Volumen, 4, "Por volumen"), (Manual, 5, "A mano"),
    ];

    public static string Texto(int metodo) => Todos.FirstOrDefault(m => m.Numero == metodo).Texto ?? metodo.ToString();
}

/// <summary>Lo digitado a mano para una línea de recepción con el método <c>Manual</c> (<c>manualAllocations[]</c>).</summary>
public sealed record RepartoManualRequest(Guid ReceiptLinePublicId, decimal Amount);

/// <summary>Una línea de recepción a la que le toca parte de los costos adicionales (<c>allocations[].receiptLine</c>).</summary>
public sealed record LineaDeRecepcionDelRepartoDto(Guid ReceiptPublicId, string? ReceiptDisplayNumber, Guid LinePublicId, int LineNumber);

/// <summary>
/// La porción de una línea de recepción (<c>LandedCostAllocationDto</c>): la base según el método, lo asignado (con el residuo del
/// redondeo si le tocó), lo que va a inventario y lo que va a costo de venta.
/// </summary>
public sealed record RepartoDeCostoAdicionalDto(
    LineaDeRecepcionDelRepartoDto ReceiptLine,
    ReferenciaDeInventarioDto Product,
    decimal Basis,
    decimal Allocated,
    decimal RoundingResidue,
    decimal ToInventory,
    decimal ToCostOfSales);

/// <summary>
/// <c>landedCost</c> del borrador y del detalle (§14.9): la factura del flete o del seguro, el método (número), el monto, lo que queda sin
/// repartir de esa factura sin contar este documento, el reparto y el residuo total. Sin reparto si no se pudo: el porqué va en los avisos.
/// </summary>
public sealed record CostosAdicionalesDto(
    DocumentoReferidoDeInventarioDto SupplierInvoice,
    int Method,
    decimal Amount,
    decimal Available,
    IReadOnlyList<RepartoDeCostoAdicionalDto> Allocations,
    decimal RoundingResidue);

/// <summary>El cuerpo de <c>POST /orders/{id}/send</c>: a qué correo (nulo = el del proveedor en el maestro).</summary>
public sealed record EnviarOrdenRequest(string? Email);

/// <summary>El cuerpo de <c>POST /orders/{id}/close-balance</c>.</summary>
public sealed record CerrarSaldoRequest(string Reason);

/// <summary>El cuerpo de <c>POST /supplier-invoices/{id}/radian-events/emit</c>: los eventos (30, 32).</summary>
public sealed record EmitirEventosRadianRequest(IReadOnlyList<int> EventCodes);

/// <summary>Un evento que el ERP emite por el canal (<c>RadianEmissionEventDto</c>): <see cref="Status"/> es el del documento electrónico.</summary>
public sealed record EventoRadianEmitidoDto(int EventCode, Guid ElectronicDocumentPublicId, int Status);

/// <summary>La respuesta 202 de la emisión (<c>RadianEmissionDto</c>).</summary>
public sealed record EmisionRadianDto(IReadOnlyList<EventoRadianEmitidoDto> Events);
