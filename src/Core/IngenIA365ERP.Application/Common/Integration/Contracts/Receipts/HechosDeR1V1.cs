using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, R1 (contracts/contabilidad.md §3; mensajes.md §2.5): los hechos de Recaudo que trae la entrega R1, con la forma común (líneas por rol, sin cuentas).
// Cada fase suma los suyos: la 25 el recaudo pendiente de aplicar. (nuevo)

/// <summary>
/// <c>RecaudoPendienteReconocido</c> v1 (<c>RPR</c>; FR-046, D14): el dinero de un recibo que no pudo ir a su contrato porque el módulo no está activado y la persona está
/// en una carga aplicada sin activar: rol del medio (caja, banco, medio por cobrar) → <c>RecaudoPendienteDeAplicar</c> con la persona como tercero. En línea. (nuevo)
/// </summary>
public sealed record RecaudoPendienteReconocidoV1
{
    public const string Type = "RecaudoPendienteReconocido";

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public Guid PendingApplicationPublicId { get; init; }

    public Guid PersonPublicId { get; init; }

    /// <summary><c>Loan</c> o <c>Account</c>.</summary>
    public string TargetModule { get; init; } = string.Empty;

    public string? ProductReference { get; init; }

    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>RecaudoPendienteAplicado</c> v1 (<c>RPA</c>; FR-046): el pendiente aplicado por el contrato del módulo ya activado: <c>RecaudoPendienteDeAplicar</c> →
/// <c>PuenteDelModulo</c>. En línea. (nuevo)
/// </summary>
public sealed record RecaudoPendienteAplicadoV1
{
    public const string Type = "RecaudoPendienteAplicado";

    public Guid PendingApplicationPublicId { get; init; }

    public Guid ReceiptPublicId { get; init; }

    public string ReceiptNumber { get; init; } = string.Empty;

    public Guid PersonPublicId { get; init; }

    public Guid ProductPublicId { get; init; }

    public string? ProductNumber { get; init; }

    public string? ModuleVoucherNumber { get; init; }

    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    public UserRefV1 AppliedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>EgresoDeCajaEntregado</c> v1 (<c>EGE</c>; FR-051 a FR-056; contabilidad.md §3): el dinero que la caja entregó con la autorización del módulo. Débito al rol de
/// lo que se paga —el puente del módulo (retiro, cancelación con entrega, saldo a favor de Cartera), <c>SaldoAFavorDePagadores</c> o <c>ConceptoVario</c>— y crédito a
/// <c>Caja</c>. Tipo de comprobante sugerido <c>EG</c>; en línea. (nuevo)
/// </summary>
public sealed record EgresoDeCajaEntregadoV1
{
    public const string Type = "EgresoDeCajaEntregado";

    public Guid CashDisbursementPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary>De <c>CashDisbursementKind</c>: <c>SavingsWithdrawal</c>, <c>AccountClosingDelivery</c>, <c>LendingCreditBalanceRefund</c>…</summary>
    public string Kind { get; init; } = string.Empty;

    public Guid BeneficiaryPersonPublicId { get; init; }

    public Guid? ReceivedByPersonPublicId { get; init; }

    public Guid? ProductPublicId { get; init; }

    public string? ProductNumber { get; init; }

    public string? ModuleVoucherNumber { get; init; }

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public UserRefV1 DeliveredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>GmfDeEgresoCausado</c> v1 (<c>GEC</c>; FR-205): el GMF de un egreso de caja que dispone de una cuenta contable a favor de un tercero, por los parámetros
/// <c>Gmf.*</c> de <c>RCD</c>; nunca el que el origen ya liquidó. La cooperativa lo asume: <c>GmfGasto</c> → <c>GmfPorPagar</c>. Tipo <c>EG</c>; en línea. (nuevo)
/// </summary>
public sealed record GmfDeEgresoCausadoV1
{
    public const string Type = "GmfDeEgresoCausado";

    public Guid CashDisbursementPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public decimal TaxBase { get; init; }

    public decimal Rate { get; init; }

    public decimal Amount { get; init; }

    /// <summary>El motivo sellado (<c>DevolucionGravada</c>, <c>EgresoGravado</c>).</summary>
    public string Reason { get; init; } = string.Empty;

    public DateOnly OperationDate { get; init; }

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>EgresoDeCajaAnulado</c> v1 (<c>EGA</c>; FR-061): el espejo del egreso y de su GMF cuando el dinero volvió a la caja, en la sesión abierta. Tipo <c>AR</c>; en
/// línea. (nuevo)
/// </summary>
public sealed record EgresoDeCajaAnuladoV1
{
    public const string Type = "EgresoDeCajaAnulado";

    public Guid CashDisbursementVoidingPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid CashDisbursementPublicId { get; init; }

    public string CashDisbursementNumber { get; init; } = string.Empty;

    public string? ModuleReversalNumber { get; init; }

    public decimal Amount { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateOnly VoidedOn { get; init; }

    public UserRefV1 VoidedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
