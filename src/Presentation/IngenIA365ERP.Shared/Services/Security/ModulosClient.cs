using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Cliente tipado de los módulos por cooperativa del maestro (feature 020, US1; contracts/api.md §1): <c>GET/PUT
/// /api/saas/tenants/{tenantId}/modules</c>. La cabecera <c>Authorization</c> la pone el handler (<c>ElTokenDeSesionLoPoneElHandler</c>).
/// </summary>
public sealed class ModulosClient(HttpClient http, CentralAuthClient auth)
{
    public Task<ResultadoDeInventario<IReadOnlyList<ModuloDeCooperativaDto>>> ListarAsync(Guid cooperativa, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<IReadOnlyList<ModuloDeCooperativaDto>>(http, auth, HttpMethod.Get,
            $"/api/saas/tenants/{cooperativa}/modules", null, null, ct);

    public Task<ResultadoDeInventario<object>> CambiarAsync(Guid cooperativa, string codigo, bool habilitado, string motivo, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<object>(http, auth, HttpMethod.Put,
            $"/api/saas/tenants/{cooperativa}/modules/{Uri.EscapeDataString(codigo)}", new { enabled = habilitado, reason = motivo }, null, ct);
}

/// <summary>Un módulo habilitable y su estado en una cooperativa.</summary>
public sealed record ModuloDeCooperativaDto(string Code, string Name, bool Enabled, DateTime? ChangedAt, string? ChangedBy, string? Reason);
