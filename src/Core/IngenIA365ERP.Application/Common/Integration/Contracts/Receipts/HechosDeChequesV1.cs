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

/// <summary>
/// <c>ChequeRecibidoPagado</c> v1 (<c>CRP</c>; segunda ronda del 2026-10-27, D-l; FR-235): el cheque recibido quedó pagado —por el cruce del canje con el abono del extracto
/// o a mano—: <c>BancoRecaudo</c> de la cuenta propia que recibió el abono → <c>ChequesEnCanje</c>. En línea. Hasta esa fecha pagar un cheque no emitía hecho y lo que el
/// recibo debitó a <c>ChequesEnCanje</c> nunca pasaba al banco. (nuevo)
/// </summary>
public sealed record ChequeRecibidoPagadoV1
{
    public const string Type = "ChequeRecibidoPagado";

    public Guid ReceivedCheckPublicId { get; init; }

    public string CheckNumber { get; init; } = string.Empty;

    public Guid? ReceiptPublicId { get; init; }

    public string? ReceiptNumber { get; init; }

    public Guid OwnBankAccountPublicId { get; init; }

    public Guid? StatementLinePublicId { get; init; }

    public string? ClearingBatchNumber { get; init; }

    public decimal Amount { get; init; }

    public DateOnly PaidOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
