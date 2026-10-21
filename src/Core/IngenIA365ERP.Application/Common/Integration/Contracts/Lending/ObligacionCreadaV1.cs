namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>ObligacionCreada</c> v1 (§11.1; <c>CAR → Inventory</c>): la respuesta a Comercial al crear la obligación de una venta (o de un
/// reemplazo, C17). Origen el recibo del mensaje de Comercial, clave <c>Confirmation</c>, <c>CadenaDeOrden</c> = la venta. Admite
/// proceso. Comercial guarda número y estado, nunca saldos ni cuotas (T18).
/// </summary>
public sealed record ObligacionCreadaV1
{
    public const string Type = "ObligacionCreada";

    /// <summary>El mensaje de Comercial al que responde.</summary>
    public SaleMessageRefV1 SaleMessage { get; init; } = new();

    /// <summary>La venta (FR-053).</summary>
    public ExternalDocumentRefV1 Invoice { get; init; } = new();

    /// <summary>El pago a crédito de la venta: una obligación por pago.</summary>
    public Guid CreditPaymentPublicId { get; init; }

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>La línea usada (FR-050).</summary>
    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>Valor financiado (cero en <c>CancelledBeforeCutoff</c>).</summary>
    public decimal Amount { get; init; }

    /// <summary>Fecha de la venta.</summary>
    public DateOnly OriginDate { get; init; }

    /// <summary><c>Validated</c>, <c>ValidationFailed</c>, <c>Migrated</c> o <c>CancelledBeforeCutoff</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Con <c>ValidationFailed</c>: el motivo en español (FR-052).</summary>
    public SaleValidationV1? Validation { get; init; }

    /// <summary>Resumen del plan; nulo en <c>CancelledBeforeCutoff</c>.</summary>
    public LoanPlanSummaryV1? Plan { get; init; }

    /// <summary><c>Reservation</c>, <c>Assigned</c> o <c>Migration</c>.</summary>
    public string NumberSource { get; init; } = string.Empty;

    /// <summary>La reserva usada (v2).</summary>
    public Guid? ReservationPublicId { get; init; }

    /// <summary>En un reemplazo, el crédito anulado (FR-054).</summary>
    public LoanRefV1? ReplacesLoan { get; init; }
}

/// <summary>El motivo de una validación fallida (§11.1, <c>validation</c>).</summary>
public sealed record SaleValidationV1
{
    public string Code { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
