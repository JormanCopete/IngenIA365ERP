namespace IngenIA365ERP.Domain.Enums.Transport;

// Feature 020 (data-model §2): los enums de Transporte. Se guardan como smallint; un valor nunca se renumera.

/// <summary>Estado de un vehículo (FR-009). Retirado es terminal: el reingreso es un vehículo nuevo (FR-010).</summary>
public enum VehicleStatus : short
{
    Active = 1,
    Inactive = 2,
    Retired = 3,
}

/// <summary>Qué cambió en un evento de la historia del vehículo.</summary>
public enum VehicleEventKind : short
{
    Registered = 1,
    StatusChanged = 2,
    OwnerChanged = 3,
    Reentered = 4,
}

/// <summary>Clase de vinculación del propietario: se deduce, no se digita (FR-016).</summary>
public enum AffiliationClass : short
{
    Associate = 1,
    External = 2,
}

/// <summary>Los nueve catálogos de Transporte de <c>TRN_CatalogItems</c> (FR-024, R6).</summary>
public enum TransportCatalogKind : short
{
    Brand = 1,
    VehicleClass = 2,
    BodyType = 3,
    Fuel = 4,
    Modality = 5,
    ServiceClass = 6,
    TransitAuthority = 7,
    RollingGroup = 8,
    EquipmentType = 9,
}

/// <summary>Estado de un equipo del vehículo.</summary>
public enum EquipmentStatus : short
{
    Active = 1,
    Inactive = 2,
}

/// <summary>Estado del registro de conductor.</summary>
public enum DriverStatus : short
{
    Active = 1,
    Retired = 2,
}

/// <summary>De dónde vino un cambio: la pantalla o una plantilla.</summary>
public enum ChangeSource : short
{
    Screen = 1,
    Import = 2,
}

/// <summary>Una corrida de facturación de todo el período o de un vehículo.</summary>
public enum BillingMode : short
{
    Batch = 1,
    Single = 2,
}

/// <summary>A crédito (queda por cobrar) o de contado (se paga al facturar).</summary>
public enum BillingPaymentMode : short
{
    Credit = 1,
    Cash = 2,
}

/// <summary>Estado de una corrida de facturación.</summary>
public enum BillingRunStatus : short
{
    Queued = 1,
    Running = 2,
    Finished = 3,
    FinishedWithOmissions = 4,
    Failed = 5,
}

/// <summary>Política de cobro de una suscripción que empieza o termina dentro del mes (FR-034).</summary>
public enum ProrationPolicy : short
{
    FullMonth = 1,
    Prorated = 2,
}

/// <summary>Por dónde se cobra un cargo de servicio.</summary>
public enum ChargeRoute : short
{
    /// <summary>Obligación de Cartera por servicio (FR-058).</summary>
    Lending = 1,

    /// <summary>Cuenta por cobrar comercial, sin Cartera activa (FR-059).</summary>
    CommercialReceivable = 2,

    /// <summary>De contado, al facturar.</summary>
    Cash = 3,
}

/// <summary>Estado del cargo de un servicio a un vehículo en un período.</summary>
public enum ChargeStatus : short
{
    Open = 1,
    Paid = 2,
    Voided = 3,
}

/// <summary>Lo que pide revisión en un cargo (R14).</summary>
public enum ChargeReviewFlag : short
{
    None = 0,
    PaymentVoidedAfterNotice = 1,
    PaidAgainAfterVoid = 2,
    NoConnector = 3,
}

/// <summary>De dónde viene un importe asignado a un cargo (R13).</summary>
public enum AllocationSource : short
{
    LendingPayment = 1,
    CommercialReceipt = 2,
    CashAtInvoice = 3,
}
