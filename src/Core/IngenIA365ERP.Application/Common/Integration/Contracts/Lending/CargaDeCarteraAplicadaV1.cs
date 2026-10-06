using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CargaDeCarteraAplicada</c> v1 (§10.23; <c>CAP</c>; informativo, con recibo): origen la carga (<c>LND_MigrationLoads</c>),
/// clave <c>Confirmation</c>.
/// </summary>
public sealed record CargaDeCarteraAplicadaV1
{
    public const string Type = "CargaDeCarteraAplicada";

    public Guid MigrationLoadPublicId { get; init; }

    public DateOnly CutoffDate { get; init; }

    public Guid? ReplacesLoadPublicId { get; init; }

    public int LoanCount { get; init; }

    public decimal PrincipalTotal { get; init; }

    public int DifferencesCount { get; init; }

    public UserRefV1 AppliedBy { get; init; } = new();
}
