using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US14 (T0242; FR-189, FR-203; contracts/contabilidad.md §3): el dinero que la caja retiene por una coincidencia no vinculante y su liberación. Sin cuentas.

/// <summary>
/// <c>DineroRetenidoEnCustodia</c> v1 (<c>DCU</c>): el efectivo de una operación retenida por cumplimiento queda en la caja en custodia: <c>Caja</c> → <c>CustodiaPorLista</c>.
/// En línea. (nuevo)
/// </summary>
public sealed record DineroRetenidoEnCustodiaV1
{
    public const string Type = "DineroRetenidoEnCustodia";

    public Guid MovementPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid OperationPublicId { get; init; }

    public Guid SessionPublicId { get; init; }

    public Guid PayerPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>CustodiaLiberada</c> v1 (<c>CLB</c>): el oficial libera la custodia para que la operación siga (<c>CustodiaPorLista</c> → <c>Caja</c>, con su movimiento de salida;
/// el recibo vuelve a entrar el efectivo) o la devuelve por orden de pago (<c>CustodiaPorLista</c> → <c>PuenteDeDevolucion</c>; el efectivo sigue en la caja hasta la
/// remesa). En línea. (nuevo)
/// </summary>
public sealed record CustodiaLiberadaV1
{
    public const string Type = "CustodiaLiberada";

    public Guid ReleasePublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid OperationPublicId { get; init; }

    public string Destination { get; init; } = string.Empty;

    public Guid? PaymentOrderPublicId { get; init; }

    public Guid PayerPersonPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public Guid SupportAttachmentPublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
