using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>PagoReversadoPorCartera</c> v1 (interfaces.md §3.15, mensajes.md §11.5; <c>CAR → Receipts</c>, el único tipo pensado para
/// varios destinos): el reverso de un pago iniciado en Cartera, al destino del recaudador (<c>SolicitudDeEmision.Destino</c>). Origen
/// <c>PaymentReversal</c>, clave <c>Confirmation</c>.
/// </summary>
public sealed record PagoReversadoPorCarteraV1
{
    public const string Type = "PagoReversadoPorCartera";

    /// <summary>Crédito.</summary>
    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>El <c>PagoAplicado</c> reversado: mensaje, operación y número.</summary>
    public OriginalFactRefV1 Payment { get; init; } = new();

    /// <summary>La operación de reverso de Cartera.</summary>
    public Guid ReversalOperationPublicId { get; init; }

    /// <summary>Su número visible.</summary>
    public string ReversalNumber { get; init; } = string.Empty;

    /// <summary>Recaudador, la clave con que aplicó y su recibo original.</summary>
    public CollectorRefV1 Collector { get; init; } = new();

    public decimal ReversedAmount { get; init; }

    /// <summary>Fecha valor del pago.</summary>
    public DateOnly ValueDate { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Quien reversó.</summary>
    public UserRefV1 ReversedBy { get; init; } = new();

    /// <summary>Excedente ya devuelto: lo resuelve el recaudador (FR-081).</summary>
    public decimal SurplusAlreadyReturned { get; init; }

    /// <summary>El crédito vuelve a quedar vivo.</summary>
    public bool Reopened { get; init; }
}
