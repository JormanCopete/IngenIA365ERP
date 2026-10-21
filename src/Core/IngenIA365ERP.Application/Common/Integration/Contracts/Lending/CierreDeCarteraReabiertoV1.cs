using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CierreDeCarteraReabierto</c> v1 (§10.23; <c>CRA</c>; informativo, con recibo): origen el cierre que se reabre, clave
/// <c>Reopen:{versión}</c>. Admite proceso.
/// </summary>
public sealed record CierreDeCarteraReabiertoV1
{
    public const string Type = "CierreDeCarteraReabierto";

    public int Year { get; init; }

    public int Month { get; init; }

    public int ReopenedCloseVersion { get; init; }

    public Guid ReopeningPublicId { get; init; }

    public string Reason { get; init; } = string.Empty;

    public UserRefV1 ReopenedBy { get; init; } = new();
}
