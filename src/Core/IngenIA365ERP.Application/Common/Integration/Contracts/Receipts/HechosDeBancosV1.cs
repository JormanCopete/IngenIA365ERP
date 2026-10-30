using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US6 (T0196; FR-068, FR-070 a FR-073, FR-199; contracts/contabilidad.md §3): los hechos de los abonos del extracto —el recibo de canal banco, la partida
// por identificar, su clasificación y su devolución al consignante—. Sin cuentas: roles y dimensiones por código; la cuenta bancaria propia por su PublicId.

/// <summary>
/// <c>ReciboDeBancoEmitido</c> v1 (<c>RBE</c>): el recibo de canal banco aplicado en forma no interactiva: débito a <c>BancoRecaudo</c> (o a <c>PartidasPorIdentificar</c> si
/// identifica una partida) y crédito al puente del módulo o al concepto vario; el excedente, a <c>SaldoAFavorDePagadores</c>. En línea. (nuevo)
/// </summary>
public sealed record ReciboDeBancoEmitidoV1
{
    public const string Type = "ReciboDeBancoEmitido";

    public Guid ReceiptPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public DateOnly ValueDate { get; init; }

    /// <summary>La fecha del movimiento cuando el contrato no la admitió y se aplicó con la más antigua que admite (FR-071, R0).</summary>
    public DateOnly? MovementDate { get; init; }

    public Guid? BankStatementLinePublicId { get; init; }

    public Guid? UnidentifiedItemPublicId { get; init; }

    public Guid PayerPersonPublicId { get; init; }

    public decimal TotalApplied { get; init; }

    public decimal Surplus { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<AplicacionDeReciboV1> Applications { get; init; } = [];

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary><c>PartidaPorIdentificarReconocida</c> v1 (<c>PIR</c>): un abono sin persona queda como pasivo: <c>BancoRecaudo</c> → <c>PartidasPorIdentificar</c>. (nuevo)</summary>
public sealed record PartidaPorIdentificarReconocidaV1
{
    public const string Type = "PartidaPorIdentificarReconocida";

    public Guid UnidentifiedItemPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid? BankStatementLinePublicId { get; init; }

    public DateOnly MovementDate { get; init; }

    public decimal Amount { get; init; }

    public string? Reference { get; init; }

    public string? DepositorName { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>PartidaClasificada</c> v1 (<c>PCL</c>): un abono (o una partida) que no es recaudo se clasifica a un concepto con aprobación —rendimiento, reintegro, giro
/// devuelto, o la decisión registrada de llevar una partida vencida a ingreso—: <c>PartidasPorIdentificar</c> o <c>BancoRecaudo</c> → <c>ConceptoVario</c>. (nuevo)
/// </summary>
public sealed record PartidaClasificadaV1
{
    public const string Type = "PartidaClasificada";

    public Guid DocumentPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary><c>BankItem</c> (el abono del extracto) o <c>UnidentifiedItem</c> (una partida).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary><c>ClassifiedToConcept</c>, <c>ReturnedDisbursement</c> o <c>MovedToIncome</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public string MiscConceptCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    /// <summary>La orden de pago del giro devuelto (FR-199).</summary>
    public Guid? PaymentOrderPublicId { get; init; }

    public Guid? ApprovalRequestPublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>PartidaDevueltaAlConsignante</c> v1 (<c>PDC</c>, nuevo; DT §2.7): la partida se devuelve al consignante con una orden de pago a Tesorería:
/// <c>PartidasPorIdentificar</c> → <c>PuenteDeDevolucion</c>. El giro lo contabiliza Tesorería (<c>PagoDeOrdenGirado</c> contra el mismo puente). (nuevo)
/// </summary>
public sealed record PartidaDevueltaAlConsignanteV1
{
    public const string Type = "PartidaDevueltaAlConsignante";

    public Guid UnidentifiedItemPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid PaymentOrderPublicId { get; init; }

    public string PaymentOrderNumber { get; init; } = string.Empty;

    public Guid BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly RefundedOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
