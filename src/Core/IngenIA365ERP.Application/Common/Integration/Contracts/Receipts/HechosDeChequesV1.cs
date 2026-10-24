using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US15 (T0218; FR-060, FR-085; contracts/contabilidad.md §3): el cheque recibido devuelto. Sin cuentas: roles y dimensiones por código.

/// <summary>
/// <c>ChequeRecibidoDevuelto</c> v1 (<c>CRD</c>): por cada porción reversada, el puente del módulo (o el concepto vario) → <c>ChequesEnCanje</c>; lo que el cheque respaldaba y
/// ya se entregó, <c>CuentaPorCobrarPagador</c> → <c>ChequesEnCanje</c>. En línea, enlazado al recibo original. (nuevo)
/// </summary>
public sealed record ChequeRecibidoDevueltoV1
{
    public const string Type = "ChequeRecibidoDevuelto";

    public Guid ReceivedCheckPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string CheckNumber { get; init; } = string.Empty;

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public OriginalFactRefV1 OriginalFact { get; init; } = new();

    public string ReturnCauseCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public decimal PayerReceivable { get; init; }

    public DateOnly ReturnedOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
