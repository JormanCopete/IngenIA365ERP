using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Domain.Entities.Transport;

/// <summary>
/// Un producto de Comercial que es un servicio por vehículo (<c>TRN_Services</c>; feature 020, R9; FR-028, FR-044, FR-056, FR-058):
/// radio, app u otro. Dice su prioridad (orden de líneas, pagos y reparto), la línea de Cartera de su obligación, los medios de pago a
/// crédito para asociados y externos y, si notifica, su conector. Un producto es a lo sumo un servicio vivo.
/// </summary>
public class TransportService : AuditableEntity
{
    public Guid ProductPublicId { get; set; }

    /// <summary>El código del producto al configurar (para mostrar y para las plantillas; el producto manda).</summary>
    public string ProductCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>1 = primero.</summary>
    public int Priority { get; set; } = 1;

    /// <summary>La línea de Cartera (marcada de servicio, sin interés) de la obligación de este servicio; nula sin Cartera.</summary>
    public string? CreditLineCode { get; set; }

    /// <summary>El medio de pago a crédito para propietarios asociados (clase <c>AssociateCredit</c>).</summary>
    public string? AssociateCreditMeansCode { get; set; }

    /// <summary>El medio de pago a crédito para propietarios externos (clase <c>CustomerCredit</c>).</summary>
    public string? CustomerCreditMeansCode { get; set; }

    /// <summary>El conector que avisa al sistema externo cuando el servicio queda pagado; nulo = no notifica.</summary>
    public int? ConnectorId { get; set; }
    public Connector? Connector { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>La suscripción de un vehículo a un servicio, con vigencia (<c>TRN_Subscriptions</c>; FR-029).</summary>
public class VehicleSubscription : AuditableEntity
{
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int ServiceId { get; set; }
    public TransportService? Service { get; set; }
    public DateOnly From { get; set; }
    public DateOnly? To { get; set; }
    public string? Reason { get; set; }

    public ICollection<SubscriptionPrice> Prices { get; init; } = [];

    public bool SeCruzaCon(DateOnly desde, DateOnly? hasta) => From <= (hasta ?? DateOnly.MaxValue) && desde <= (To ?? DateOnly.MaxValue);
}

/// <summary>Un precio pactado de la suscripción, con vigencia sin cruces (<c>TRN_SubscriptionPrices</c>; FR-029).</summary>
public class SubscriptionPrice : AuditableEntity
{
    public int SubscriptionId { get; set; }
    public VehicleSubscription? Subscription { get; set; }
    public decimal Price { get; set; }
    public DateOnly From { get; set; }
    public DateOnly? To { get; set; }

    public bool VigenteEn(DateOnly fecha) => From <= fecha && (To is null || To >= fecha);
}

/// <summary>
/// La política de facturación de servicios de la cooperativa, con vigencia (<c>TRN_BillingPolicies</c>; FR-034): cobro completo o
/// proporcional a mitad de mes, el tipo de documento de venta de Comercial con que factura, la bodega o el punto de venta que ese tipo
/// exija y el día de vencimiento de la cuota.
/// </summary>
public class BillingPolicy : AuditableEntity
{
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public ProrationPolicy Proration { get; set; } = ProrationPolicy.FullMonth;
    public string SalesDocumentTypeCode { get; set; } = string.Empty;
    public string? WarehouseCode { get; set; }
    public string? SalesPointCode { get; set; }

    /// <summary>Día del mes en que vence la cuota (1..28); 0 = el último día del mes.</summary>
    public short DueDay { get; set; }

    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>Una corrida de facturación de servicios (<c>TRN_BillingRuns</c>; FR-030 a FR-033).</summary>
public class BillingRun : AuditableEntity
{
    /// <summary><c>aaaamm</c>.</summary>
    public string Period { get; set; } = string.Empty;
    public BillingMode Mode { get; set; }
    public BillingPaymentMode PaymentMode { get; set; }
    public BillingRunStatus Status { get; set; } = BillingRunStatus.Queued;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string StartedBy { get; set; } = string.Empty;

    /// <summary>La identidad central de quien la lanzó: la tarea de fondo emite a su nombre.</summary>
    public Guid? StartedByCentralUserId { get; set; }
    public int InvoicesCount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Los omitidos con su motivo (<c>[{ vehicle, plate, code, reason }]</c>) y los que ya estaban.</summary>
    public string? SummaryJson { get; set; }

    /// <summary>La clave de idempotencia del lanzamiento.</summary>
    public Guid OperationKey { get; set; }

    /// <summary>Si es individual, el vehículo.</summary>
    public int? VehicleId { get; set; }

    /// <summary>El último vehículo procesado (por <c>Id</c>): la corrida en lote avanza por tandas desde aquí.</summary>
    public int? CursorVehicleId { get; set; }

    /// <summary>Cuántos vehículos ya tenían su factura del período.</summary>
    public int AlreadyBilledCount { get; set; }

    /// <summary>Cuántos vehículos se omitieron (el detalle va en <see cref="SummaryJson"/>).</summary>
    public int OmittedCount { get; set; }
}

/// <summary>
/// El cargo de un servicio a un vehículo en un período (<c>TRN_ServiceCharges</c>; feature 020, R10, R13, R14): la línea de la factura
/// que lo cobra, la forma de pago que lo respalda (y con ella su obligación de Cartera) y su estado. Es la marca de «ya facturado»:
/// único por vehículo, servicio y período entre los no anulados (FR-031). <see cref="ExternalId"/> nace con el cargo y nunca cambia:
/// es el identificador que recibe el sistema externo en todos los reintentos (FR-048).
/// </summary>
public class ServiceCharge : AuditableEntity
{
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int ServiceId { get; set; }
    public TransportService? Service { get; set; }

    /// <summary><c>aaaamm</c>.</summary>
    public string Period { get; set; } = string.Empty;
    public int BillingRunId { get; set; }
    public BillingRun? BillingRun { get; set; }
    public int OwnerPersonId { get; set; }
    public Person? OwnerPerson { get; set; }
    public Guid InvoicePublicId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid InvoiceLinePublicId { get; set; }

    /// <summary>La forma de pago del servicio en la factura (a crédito o de contado).</summary>
    public Guid? PaymentPublicId { get; set; }

    /// <summary>La obligación de Cartera, cuando se conoce (R12).</summary>
    public Guid? LoanPublicId { get; set; }

    public decimal Amount { get; set; }
    public ChargeRoute Route { get; set; }
    public ChargeStatus Status { get; set; } = ChargeStatus.Open;
    public DateTime? PaidAt { get; set; }

    /// <summary>El identificador para el sistema externo: se genera al crear y no cambia nunca.</summary>
    public Guid ExternalId { get; init; } = Guid.NewGuid();

    /// <summary>El envío (<c>COR_ConnectorRequests.PublicId</c>) si ya se notificó.</summary>
    public Guid? NotifiedRequestPublicId { get; set; }

    public ChargeReviewFlag ReviewFlag { get; set; } = ChargeReviewFlag.None;

    public ICollection<ChargeAllocation> Allocations { get; init; } = [];
}

/// <summary>
/// Un importe aplicado a un cargo (<c>TRN_ChargeAllocations</c>; feature 020, R13): de un pago de Cartera, de un recibo comercial o
/// del contado al facturar. Una reversa es otra fila, negativa, que apunta a la original. El mismo mensaje no asigna dos veces.
/// </summary>
public class ChargeAllocation : AuditableEntity, IHechoInmutable
{
    public int ChargeId { get; init; }
    public ServiceCharge? Charge { get; init; }
    public AllocationSource Source { get; init; }

    /// <summary>El mensaje de la bandeja que la originó; nulo en el contado al facturar.</summary>
    public Guid? SourceMessagePublicId { get; init; }
    public Guid? ReceiptPublicId { get; init; }
    public string? ReceiptNumber { get; init; }
    public decimal Amount { get; init; }
    public int? ReversesAllocationId { get; init; }
    public DateTime AppliedAt { get; init; }
    public DateOnly? ValueDate { get; init; }
}
