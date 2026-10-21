namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// El cargo de una cuenta tal como lo factura Comercial (feature 014, US4, T112; contracts/mensajes.md §5.5): la clase, el código y el nombre del cargo, el
/// evento que lo disparó y el período de un cargo periódico.
/// </summary>
public sealed record DepositChargeInvoiceRefV1
{
    public Guid ChargePublicId { get; init; }

    /// <summary>Valor de <c>ChargeKind</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>El código del servicio en Comercial que lo factura (<c>CommercialItemCode</c> del cargo).</summary>
    public string? CommercialItemCode { get; init; }

    public string? Event { get; init; }

    public DateOnly? PeriodFrom { get; init; }

    public DateOnly? PeriodTo { get; init; }
}

/// <summary>
/// <c>CargoDeCaptacionCausado</c> v1 (feature 014, US4, T112, T116; contracts/mensajes.md §1.2 #38 y §5.5; FR-128, R14, D20 de la 013): un cargo
/// <b>gravado con IVA</b> se causó en una cuenta y Comercial lo factura electrónicamente con su IVA —la Cartera no calcula IVA ni emite facturas—.
/// <c>CAR → Inventory</c>, origen el cargo (<c>AccountCharge</c>), clave <c>Confirmation</c>, tercero el titular (el adquirente). Admite proceso: los
/// cargos periódicos los causa el ciclo diario.
/// </summary>
public sealed record CargoDeCaptacionCausadoV1
{
    public const string Type = "CargoDeCaptacionCausado";

    public DepositChargeInvoiceRefV1 Charge { get; init; } = new();

    /// <summary>La cuenta del asociado (no una cuenta del plan).</summary>
    public MemberAccountRefV1 Account { get; init; } = new();

    /// <summary>El adquirente.</summary>
    public Guid HolderPersonPublicId { get; init; }

    /// <summary>El valor del cargo: la base del IVA, que calcula Comercial con su catálogo tributario.</summary>
    public decimal AmountBeforeTax { get; init; }

    /// <summary>El concepto tributario del cargo en el catálogo de Core (el servicio de Comercial que lo factura).</summary>
    public string TaxConceptCode { get; init; } = string.Empty;

    /// <summary><c>Collected</c> o <c>Pending</c>.</summary>
    public string Status { get; init; } = string.Empty;

    public DateOnly CausedOn { get; init; }
}

/// <summary>
/// <c>CargoDeCaptacionReversado</c> v1 (feature 014, US4, T112, T116; contracts/mensajes.md §1.2 #39 y §5.5): el reverso de un cargo gravado, para que
/// Comercial emita la nota crédito. <c>CAR → Inventory</c>, origen el cargo, clave <c>Confirmation:{cargo:N}</c>, <c>original</c> = el
/// <c>CargoDeCaptacionCausado</c> que se anula. Lo origina la persona que reversa.
/// </summary>
public sealed record CargoDeCaptacionReversadoV1
{
    public const string Type = "CargoDeCaptacionReversado";

    public DepositChargeInvoiceRefV1 Charge { get; init; } = new();

    public MemberAccountRefV1 Account { get; init; } = new();

    public Guid HolderPersonPublicId { get; init; }

    public decimal AmountBeforeTax { get; init; }

    public string TaxConceptCode { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateOnly CausedOn { get; init; }

    /// <summary>El <c>CargoDeCaptacionCausado</c> que se anula (nota crédito).</summary>
    public OriginalFactRefV1 Original { get; init; } = new();

    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// <c>FacturaDeCargoRegistrada</c> v1 (feature 014, US4, T112; contracts/mensajes.md §1.3 #40 y §5.6): Comercial informa el estado de la factura (o de la
/// nota crédito) de un cargo de captación; la Cartera <b>sólo lo guarda</b> para mostrarlo —una factura rechazada por la DIAN no cambia el cargo—.
/// <c>INV → Lending</c>, informativa, clave <c>Confirmation:{cargo:N}</c> (el <c>messageId</c> del <c>CargoDeCaptacionCausado</c>). La define esta
/// especificación y la emite Comercial.
/// </summary>
public sealed record FacturaDeCargoRegistradaV1
{
    public const string Type = "FacturaDeCargoRegistrada";

    /// <summary>El <c>CargoDeCaptacionCausado</c> (o <c>…Reversado</c>) que se facturó.</summary>
    public Guid ChargeMessageId { get; init; }

    public Guid ChargePublicId { get; init; }

    /// <summary>La factura o la nota crédito en Comercial.</summary>
    public ExternalDocumentRefV1 Document { get; init; } = new();

    public string? FiscalUniqueCode { get; init; }

    /// <summary><c>Issued</c>, <c>Accepted</c>, <c>Rejected</c> o <c>Voided</c>.</summary>
    public string Status { get; init; } = string.Empty;

    public string? StatusDetail { get; init; }

    public DateOnly OccurredOn { get; init; }
}
