using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CarteraActivada</c> v1 (§10.23; <c>ACT</c>; informativo, con recibo): origen la activación (<c>LND_Activations</c>), clave
/// <c>Confirmation</c>. Lo origina la persona que activa.
/// </summary>
public sealed record CarteraActivadaV1
{
    public const string Type = "CarteraActivada";

    public DateOnly ActivationDate { get; init; }

    public DateOnly CutoffDate { get; init; }

    public Guid MigrationLoadPublicId { get; init; }

    public Guid BaseClosePublicId { get; init; }

    public int LoanCount { get; init; }

    public decimal PrincipalTotal { get; init; }

    public UserRefV1 ActivatedBy { get; init; } = new();
}
