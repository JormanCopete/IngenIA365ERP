using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>ConsignacionReversadaPorCartera</c> v1 (feature 014, US4, T112; contracts/mensajes.md §1.2 #35 y §5.2 de la 014; FR-092): cuando un usuario con
/// <c>Lending.Accounts.Reverse</c> reversa desde la Cartera una consignación que aplicó un recaudador, la Cartera se lo avisa con la clave y el recibo
/// originales para que anule su recibo. Molde de <c>PagoReversadoPorCartera</c>. Origen el reverso (<c>AccountReversal</c>), clave <c>Confirmation</c>,
/// <c>related</c> = la consignación, tercero el depositante. Va al destino del recaudador: <c>Receipts</c> (Recaudo y el doble de prueba) o
/// <c>Treasury</c> (Tesorería); lo de Nómina nunca (lo reversa Nómina). No admite proceso: lo origina la persona que reversa.
/// </summary>
public sealed record ConsignacionReversadaPorCarteraV1
{
    public const string Type = "ConsignacionReversadaPorCartera";

    /// <summary>La cuenta del asociado (no una cuenta del plan).</summary>
    public MemberAccountRefV1 Account { get; init; } = new();

    /// <summary>La consignación reversada: su hecho, su operación y su número.</summary>
    public OriginalFactRefV1 Deposit { get; init; } = new();

    /// <summary>La operación de reverso de la Cartera.</summary>
    public Guid ReversalOperationPublicId { get; init; }

    public string ReversalNumber { get; init; } = string.Empty;

    /// <summary>El recaudador con la clave y el recibo originales.</summary>
    public CollectorRefV1 Collector { get; init; } = new();

    public decimal ReversedAmount { get; init; }

    /// <summary>La fecha valor de la consignación.</summary>
    public DateOnly ValueDate { get; init; }

    public string Reason { get; init; } = string.Empty;

    public UserRefV1 ReversedBy { get; init; } = new();

    /// <summary>Siempre falso desde la Cartera: la pantalla no reversa lo que dejaría la cuenta en negativo (api.md §4).</summary>
    public bool LeftTechnicalOverdraft { get; init; }
}
