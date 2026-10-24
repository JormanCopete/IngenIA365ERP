using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

// Feature 015, US10 y US11 (T0145, T0159; FR-004, FR-142, FR-144, FR-205; contracts/contabilidad.md §2, §4): la forma común de los hechos TES → Accounting y los
// del pago de una orden. Ningún hecho lleva una cuenta contable: dimensiones por código y roles; la cuenta bancaria propia va por su PublicId y Contabilidad la
// resuelve por el vínculo (ACC_OwnBankAccountLinks). Lo fija LosHechosDeRecaudoYTesoreriaNoLlevanCuentas.

/// <summary>Las dimensiones de un hecho de Tesorería, por código (contabilidad.md §2, §5). (nuevo)</summary>
public sealed record TreasuryFactDimensionsV1
{
    /// <summary>La oficina de la cuenta propia (<c>LegacyCode</c> o su Id).</summary>
    public string BranchCode { get; init; } = string.Empty;

    /// <summary>La cuenta bancaria propia: rol <c>Banco</c> por el vínculo.</summary>
    public Guid? OwnBankAccountPublicId { get; init; }

    public string? OwnBankAccountCode { get; init; }

    /// <summary><c>LendingDisbursement</c>, <c>DepositsPayout</c>, <c>Receipts</c> (<c>PaymentOrderOrigin</c>).</summary>
    public string? OrderOrigin { get; init; }

    /// <summary>La clase de orden (<c>LoanDisbursement</c>, <c>DepositWithdrawal</c>, <c>PayerCreditBalanceRefund</c>…): resuelve el puente del origen.</summary>
    public string? OrderKind { get; init; }

    /// <summary>La forma del pago (<c>Transfer</c>, <c>Check</c>, <c>ThirdPartyPayment</c>).</summary>
    public string? Form { get; init; }

    /// <summary>La causa cuando la hay (el motivo de exención del GMF, la causa de una anulación de cheque…).</summary>
    public string? Cause { get; init; }
}

/// <summary>Una línea de un hecho de Tesorería: rol, lado, valor, tercero y cruce donde la cuenta lo exija. (nuevo)</summary>
public sealed record TreasuryFactLineV1
{
    public string Role { get; init; } = string.Empty;

    /// <summary><c>Debit</c> o <c>Credit</c>.</summary>
    public string Side { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? ThirdPartyPersonPublicId { get; init; }

    public string? CrossDocument { get; init; }

    public TreasuryFactDimensionsV1 Dimensions { get; init; } = new();
}

/// <summary>Los roles y lados de los hechos de Tesorería (contabilidad.md §5). (nuevo)</summary>
public static class RolesDeTesoreria
{
    public const string Debito = "Debit";
    public const string Credito = "Credit";

    public const string Banco = "Banco";
    public const string PuenteDeGiroDelOrigen = "PuenteDeGiroDelOrigen";
    public const string PuenteDeDevolucion = "PuenteDeDevolucion";
    public const string GmfGasto = "GmfGasto";
    public const string GmfPorPagar = "GmfPorPagar";
    public const string EfectivoEnTransito = "EfectivoEnTransito";
}

/// <summary>
/// <c>GmfDePagoCausado</c> v1 (<c>GMP</c>; contabilidad.md §4; FR-205, FR-206): el GMF de un pago que dispone de una cuenta contable a favor de un tercero (las
/// devoluciones de saldos a favor y de partidas), con su base, la tarifa vigente y la exención sellada en la orden; débito <c>GmfGasto</c>, crédito
/// <c>GmfPorPagar</c>. Lo que el origen ya liquidó no se evalúa. (nuevo)
/// </summary>
public sealed record GmfDePagoCausadoV1
{
    public const string Type = "GmfDePagoCausado";

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string OrderOrigin { get; init; } = string.Empty;

    public string OrderKind { get; init; } = string.Empty;

    public DateOnly OccurredOn { get; init; }

    public decimal TaxBase { get; init; }

    /// <summary>La tarifa como fracción (0,004).</summary>
    public decimal Rate { get; init; }

    public decimal Amount { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>PagoDeOrdenGirado</c> v1 (<c>POG</c>; contabilidad.md §4; FR-142, FR-144): el resultado «girada» de una transferencia o un pago a tercero, con la fecha del
/// débito; débito <c>PuenteDeGiroDelOrigen</c> (resuelto por <c>OrderOrigin</c> y <c>OrderKind</c> a la misma cuenta que el puente del origen:
/// <c>DesembolsoPorGirar</c>, <c>DevolucionPorPagar</c>, <c>PuenteDeDevolucion</c>), crédito <c>Banco</c> (la cuenta de origen del lote). Sin hecho en un rechazo
/// previo a «girada». (nuevo)
/// </summary>
public sealed record PagoDeOrdenGiradoV1
{
    public const string Type = "PagoDeOrdenGirado";

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public Guid OriginOrderPublicId { get; init; }

    public string OriginOrderNumber { get; init; } = string.Empty;

    public string OrderOrigin { get; init; } = string.Empty;

    public string OrderKind { get; init; } = string.Empty;

    public Guid BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    /// <summary>La fecha del débito (Q3).</summary>
    public DateOnly DebitDate { get; init; }

    public string TreasuryReference { get; init; } = string.Empty;

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}
