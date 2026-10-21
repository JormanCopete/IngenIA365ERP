using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

public sealed record GrupoDelAsociadoDto(Guid PublicId, string Code, string Name, string? Description, bool IsActive);

public sealed record GrupoDelAsociadoRequest(string? Code, string Name, string? Description);

/// <summary>
/// Grupos del asociado (<c>/api/core/associate-groups</c>): son de Core, no de Inventario; viven en este cliente porque
/// el componente común de catálogos simples (<c>CatalogoDeInventario</c>) se apoya en él para inactivar y reactivar.
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeGruposDelAsociado = "/api/core/associate-groups";

    public Task<ResultadoDeInventario<IReadOnlyList<GrupoDelAsociadoDto>>> ListarGruposDelAsociadoAsync(bool incluirInactivos = false, string? buscar = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<GrupoDelAsociadoDto>>(HttpMethod.Get, ConQuery(RutaDeGruposDelAsociado, Query(
            ("includeInactive", incluirInactivos ? "true" : null), ("search", buscar))), null, null, ct);

    public Task<ResultadoDeInventario<GrupoDelAsociadoDto>> GuardarGrupoDelAsociadoAsync(Guid? id, GrupoDelAsociadoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<GrupoDelAsociadoDto>(HttpMethod.Put, $"{RutaDeGruposDelAsociado}/{i}", request, clave, ct)
            : EnviarAsync<GrupoDelAsociadoDto>(HttpMethod.Post, RutaDeGruposDelAsociado, request, clave, ct);
}