using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US19 (T0228; FR-035, FR-105; contracts/contabilidad.md §3): la remesa que sale de la bóveda al banco. Sin cuentas.

/// <summary>
/// <c>RemesaDespachada</c> v1 (<c>RMD</c>): la bóveda despacha efectivo al banco con acta y sello de bolsa: <c>EfectivoEnTransito</c> ← <c>Boveda</c>. Tesorería lo confirma
/// después con <c>RemesaConfirmada</c>. En línea. (nuevo)
/// </summary>
public sealed record RemesaDespachadaV1
{
    public const string Type = "RemesaDespachada";

    public Guid MovementPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid VaultPublicId { get; init; }

    public Guid RemittancePublicId { get; init; }

    public string RemittanceNumber { get; init; } = string.Empty;

    public Guid OwnBankAccountPublicId { get; init; }

    public decimal Amount { get; init; }

    public string? BagSeal { get; init; }

    public DateOnly DispatchedOn { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
