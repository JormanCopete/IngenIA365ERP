using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US5 (T0178; FR-064, FR-141; contracts/contabilidad.md §3): los hechos de la anulación, el reverso del módulo y los saldos a favor. Cada uno enlaza al
// hecho original (OriginalFactRefV1) y va en el período abierto. Sin cuentas: roles y dimensiones por código.

/// <summary><c>ReciboAnulado</c> v1 (<c>RAN</c>): el espejo del recibo; si la sesión ya cerró, el puente va contra <c>SaldoAFavorDePagadores</c>. (nuevo)</summary>
public sealed record ReciboAnuladoV1
{
    public const string Type = "ReciboAnulado";

    public Guid VoidingPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public OriginalFactRefV1 OriginalFact { get; init; } = new();

    public DateOnly VoidedOn { get; init; }

    /// <summary>La sesión del recibo ya había cerrado: el dinero queda como saldo a favor del pagador.</summary>
    public bool SessionClosed { get; init; }

    public string Reason { get; init; } = string.Empty;

    public UserRefV1 ApprovedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>AplicacionReversadaPorModulo</c> v1 (<c>ARM</c>): la causa confirmada del dinero de una aplicación que el módulo reversó —recibido: <c>PuenteDelModulo</c> →
/// <c>SaldoAFavorDePagadores</c>; no recibido: <c>PuenteDelModulo</c> → el rol del medio (baja)—. (nuevo)
/// </summary>
public sealed record AplicacionReversadaPorModuloV1
{
    public const string Type = "AplicacionReversadaPorModulo";

    public Guid ModuleReversalPublicId { get; init; }

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public OriginalFactRefV1 OriginalFact { get; init; } = new();

    public string Module { get; init; } = string.Empty;

    public string ModuleReversalNumber { get; init; } = string.Empty;

    /// <summary><c>MoneyReceived</c> o <c>MoneyNotReceived</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly ConfirmedOn { get; init; }

    public UserRefV1 ConfirmedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>SaldoAFavorDePagadorResuelto</c> v1 (<c>SFP</c>): <c>SaldoAFavorDePagadores</c> → <c>PuenteDeDevolucion</c> (devuelto por orden de pago a Tesorería) o →
/// <c>PuenteDelModulo</c> (aplicado con un recibo nuevo). (nuevo)
/// </summary>
public sealed record SaldoAFavorDePagadorResueltoV1
{
    public const string Type = "SaldoAFavorDePagadorResuelto";

    public Guid CreditBalancePublicId { get; init; }

    public Guid PayerPersonPublicId { get; init; }

    /// <summary><c>RefundedByOrder</c> o <c>AppliedByReceipt</c>.</summary>
    public string Form { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? DocumentPublicId { get; init; }

    public string? DocumentNumber { get; init; }

    public DateOnly ResolvedOn { get; init; }

    public UserRefV1 ResolvedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary><c>SaldoAFavorDeCarteraDevuelto</c> v1 (<c>SFC</c>): <c>PuenteDelModulo</c> → <c>PuenteDeDevolucion</c>, devuelto por el recaudador con una orden de pago. (nuevo)</summary>
public sealed record SaldoAFavorDeCarteraDevueltoV1
{
    public const string Type = "SaldoAFavorDeCarteraDevuelto";

    public Guid ObligationPublicId { get; init; }

    public string ObligationNumber { get; init; } = string.Empty;

    public Guid LendingResolutionPublicId { get; init; }

    public string LendingResolutionNumber { get; init; } = string.Empty;

    public Guid PaymentOrderPublicId { get; init; }

    public string PaymentOrderNumber { get; init; } = string.Empty;

    public Guid BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly RefundedOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>DevolucionNoGirada</c> v1 (<c>DNG</c>; feature 015, fase 16; contabilidad.md §3): Tesorería informó rechazada, anulada o con cheque no cobrado una orden de pago
/// que Recaudo pidió para devolver dinero (un saldo a favor, una partida, el dinero retenido); el dinero vuelve a donde estaba: débito <c>PuenteDeDevolucion</c>,
/// crédito al rol de origen (<c>SaldoAFavorDePagadores</c>, <c>SaldoDePagadurias</c>, <c>PartidasPorIdentificar</c>, <c>CustodiaPorLista</c> o el
/// <c>PuenteDelModulo</c> de una devolución de Cartera). Corrige el hecho de la devolución: lleva su referencia en <c>Related</c>. (nuevo)
/// </summary>
public sealed record DevolucionNoGiradaV1
{
    public const string Type = "DevolucionNoGirada";

    public Guid PaymentOrderPublicId { get; init; }

    public string PaymentOrderNumber { get; init; } = string.Empty;

    /// <summary><c>Rejected</c>, <c>CheckNotCashed</c> o <c>Voided</c>.</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>A dónde vuelve el dinero: <c>PayerCreditBalance</c>, <c>UnidentifiedItem</c>, <c>WithheldMoney</c>, <c>LendingRefund</c>.</summary>
    public string ReturnsTo { get; init; } = string.Empty;

    public Guid? BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly OccurredOn { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
