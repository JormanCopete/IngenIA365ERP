using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

// Feature 015, US19 (T0228; FR-105, FR-134 a FR-136; contracts/contabilidad.md §4): los hechos de los cheques girados y de la remesa confirmada. Sin cuentas.

/// <summary><c>ChequeGiradoEmitido</c> v1 (<c>CHE</c>): al emitir el cheque de una orden, <c>PuenteDeGiroDelOrigen</c> → <c>Banco</c>. En línea. (nuevo)</summary>
public sealed record ChequeGiradoEmitidoV1
{
    public const string Type = "ChequeGiradoEmitido";

    public Guid IssuedCheckPublicId { get; init; }

    public long CheckNumber { get; init; }

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string? OriginOrderNumber { get; init; }

    public string OrderOrigin { get; init; } = string.Empty;

    public string OrderKind { get; init; } = string.Empty;

    public Guid BeneficiaryPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly IssuedOn { get; init; }

    public bool Crossed { get; init; }

    public bool FirstBeneficiaryOnly { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>ChequeGiradoAnulado</c> v1 (<c>CHA</c>): el espejo del emitido —<c>Banco</c> → <c>PuenteDeGiroDelOrigen</c>— con la causa <c>SpoiledBeforeDelivery</c> (anulado antes de
/// entregar) o <c>NotCashed</c> (no cobrado). En línea, enlazado al emitido. (nuevo)
/// </summary>
public sealed record ChequeGiradoAnuladoV1
{
    public const string Type = "ChequeGiradoAnulado";

    public Guid IssuedCheckPublicId { get; init; }

    public long CheckNumber { get; init; }

    public Guid PaymentOrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Cause { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public decimal Amount { get; init; }

    public DateOnly VoidedOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>RemesaConfirmada</c> v1 (<c>RMC</c>): bóveda → banco, <c>Banco</c> → <c>EfectivoEnTransito</c> al confirmar; banco → bóveda, <c>EfectivoEnTransito</c> → <c>Banco</c> al
/// sacar el efectivo del banco (la bóveda lo recibe con su movimiento). En línea. (nuevo)
/// </summary>
public sealed record RemesaConfirmadaV1
{
    public const string Type = "RemesaConfirmada";

    public Guid RemittancePublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Direction { get; init; } = string.Empty;

    public Guid VaultPublicId { get; init; }

    public decimal Amount { get; init; }

    public string? BagSeal { get; init; }

    public DateOnly ConfirmedOn { get; init; }

    public Guid? StatementLinePublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<TreasuryFactLineV1> Lines { get; init; } = [];
}
