namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>OrdenDeGiroEmitida</c> v1 (interfaces.md §4.2, mensajes.md §11.3; <c>CAR → Treasury</c>): nace en el guardado del desembolso,
/// también cuando la transferencia sale en un archivo de Cartera (<see cref="PaidByFile"/>, C10). Origen la orden, clave
/// <c>Confirmation</c>. La define esta especificación y Tesorería la adopta (D27).
/// </summary>
public sealed record OrdenDeGiroEmitidaV1
{
    public const string Type = "OrdenDeGiroEmitida";

    public Guid OrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>La obligación: <c>PublicId</c> y número.</summary>
    public LoanRefV1 Loan { get; init; } = new();

    public int TrancheNumber { get; init; }

    /// <summary><c>Transfer</c>, <c>Check</c> o <c>ThirdPartyPayment</c>.</summary>
    public string Form { get; init; } = string.Empty;

    public PayeeV1 Payee { get; init; } = new();

    public decimal GrossAmount { get; init; }

    /// <summary>Recogidas (FR-046).</summary>
    public IReadOnlyList<PayoffDeductionV1> PayoffDeductions { get; init; } = [];

    /// <summary>Lo que Tesorería gira.</summary>
    public decimal NetAmount { get; init; }

    public DateOnly PayDate { get; init; }

    /// <summary>El número del crédito.</summary>
    public string Concept { get; init; } = string.Empty;

    /// <summary>Sale en un archivo de desembolsos de Cartera (FR-047): no pagar por otra vía (C10).</summary>
    public bool PaidByFile { get; init; }

    /// <summary>Giro nuevo de una orden fallida.</summary>
    public Guid? ReplacesOrderPublicId { get; init; }
}
