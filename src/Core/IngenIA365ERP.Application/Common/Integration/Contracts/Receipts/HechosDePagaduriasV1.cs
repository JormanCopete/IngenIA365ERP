using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US7 (T0209; FR-078 a FR-080; contracts/contabilidad.md §3): los hechos del giro de una pagaduría y de su aplicación. Sin cuentas: roles y dimensiones por código.

/// <summary>
/// <c>GiroDePagaduriaRecibido</c> v1 (<c>GPR</c>): el dinero que giró la pagaduría queda como su saldo hasta aplicar sus líneas: <c>BancoRecaudo</c> (o <c>Caja</c>) →
/// <c>SaldoDePagadurias</c>. En línea. (nuevo)
/// </summary>
public sealed record GiroDePagaduriaRecibidoV1
{
    public const string Type = "GiroDePagaduriaRecibido";

    public Guid RemittancePublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid PaymasterPersonPublicId { get; init; }

    public string Source { get; init; } = string.Empty;

    public Guid? BankStatementLinePublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly RemittedOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>ReciboDePagaduriaEmitido</c> v1 (<c>RPG</c>): el recibo de canal pagaduría de una línea descontada aplicada por el contrato: <c>SaldoDePagadurias</c> → el puente del
/// módulo. En línea. (nuevo)
/// </summary>
public sealed record ReciboDePagaduriaEmitidoV1
{
    public const string Type = "ReciboDePagaduriaEmitido";

    public Guid ReceiptPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid PaymasterPersonPublicId { get; init; }

    public DateOnly ValueDate { get; init; }

    public decimal TotalApplied { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<AplicacionDeReciboV1> Applications { get; init; } = [];

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
