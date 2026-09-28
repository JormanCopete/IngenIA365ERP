namespace IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

// Feature 012, entrega I4 (T690; decisiones-transversales §2.5, data-model §18 y §26; contracts/dian.md). En la base
// toda enumeración se guarda como int con su valor numérico; la pantalla traduce. El ambiente DIAN es
// Enums/Dian/DianEnvironment (compartido con la nómina electrónica).

/// <summary>Cómo emite la cooperativa: por un proveedor tecnológico o con software propio (servicio central de la 010).</summary>
public enum EmissionMode
{
    TechnologyProvider = 1,
    OwnSoftware = 2,
}

/// <summary>Tipo de documento electrónico (contracts/dian.md §4.3).</summary>
public enum ElectronicDocumentKind
{
    Invoice = 1,
    CreditNote = 2,
    DebitNote = 3,
    PosEquivalent = 4,
    PosAdjustmentNote = 5,
    SupportDocument = 6,
    SupportDocumentAdjustmentNote = 7,
    RadianEvent030 = 8,
    RadianEvent032 = 9,
}

/// <summary>Estado fiscal del documento electrónico (contracts/dian.md §5).</summary>
public enum ElectronicDocumentStatus
{
    /// <summary>Numerado; no hay constancia de que el canal lo haya recibido. Se puede reenviar.</summary>
    Pending = 0,

    /// <summary>El canal lo recibió, o pudo recibirlo; falta la respuesta definitiva. Sólo se consulta.</summary>
    Sent = 1,

    /// <summary>Validado, con código único y respuesta de validación. Final.</summary>
    Validated = 2,

    /// <summary>Validado con notificaciones. Final.</summary>
    ValidatedWithNotices = 3,

    /// <summary>Rechazado; espera a una persona (casos a, b o c).</summary>
    Rejected = 4,

    /// <summary>Contingencia 03: papel con numeración de contingencia, pendiente de transmitir.</summary>
    IssuerContingency = 5,

    /// <summary>Contingencia 04: entregado sin validar, pendiente de transmitir.</summary>
    DianContingency = 6,

    /// <summary>Caso c: el número queda ligado a su documento anulado. Final.</summary>
    CancelledWithoutReplacement = 7,
}

/// <summary>Tipo de contingencia: 03 falla el facturador (o su canal), 04 falla la DIAN.</summary>
public enum ContingencyType
{
    Issuer03 = 3,
    Dian04 = 4,
}

/// <summary>Quién rechazó: la DIAN o el canal (el documento nunca llegó a la DIAN).</summary>
public enum RejectedBy
{
    Dian = 1,
    Channel = 2,
}

/// <summary>Tipo de resolución de numeración.</summary>
public enum ResolutionKind
{
    Invoice = 1,
    PosEquivalent = 2,
    SupportDocument = 3,
    Contingency = 4,
}

/// <summary>Resultado uniforme de una llamada al canal (contracts/dian.md §3.3).</summary>
public enum ChannelOutcome
{
    Validated = 1,
    ValidatedWithNotices = 2,
    Rejected = 3,
    InProcess = 4,
    NotFound = 5,
    DianUnavailable = 6,
    ChannelUnavailable = 7,
    InvalidData = 8,
}

/// <summary>Operación de un intento contra el canal (<c>COR_ElectronicDocumentTransmissions.Operation</c>).</summary>
public enum TransmissionOperation
{
    Emit = 1,
    TransmitContingency = 2,
    QueryStatus = 3,
    Event = 4,
    Download = 5,
}

/// <summary>Por qué nació una versión del documento electrónico.</summary>
public enum DocumentVersionReason
{
    Initial = 1,
    CaseA = 2,
    CaseB = 3,
}

/// <summary>Quién entrega el documento al comprador por correo.</summary>
public enum EmailDeliveryBy
{
    Erp = 1,
    Channel = 2,
}

/// <summary>Qué código único lleva el documento (data-model §26).</summary>
public enum UniqueCodeKind
{
    Cufe = 1,
    Cude = 2,
    Cuds = 3,
}

/// <summary>Estado de un evento de contingencia (data-model §26).</summary>
public enum ContingencyEventStatus
{
    Open = 1,
    Closed = 2,
}
