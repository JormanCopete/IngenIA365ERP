using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Tenants.GetTenantMfaPolicy;
using IngenIA365ERP.Application.Tenants.UpdateTenantMfaPolicy;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T102 — GET/PUT /api/tenants/{publicId}/mfa-policy (US4).
/// </summary>
public sealed class TenantMfaPolicyModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantPublicId:guid}/mfa-policy")
            .WithTags("Identity / Tenant MFA Policy")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", GetAsync).WithName("TenantMfaPolicy_Get");
        group.MapPut("/", UpdateAsync).WithName("TenantMfaPolicy_Update");
    }

    private static async Task<object?> GetAsync(
        Guid tenantPublicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new GetTenantMfaPolicyQuery(tenantPublicId), ct);

    private static async Task<object?> UpdateAsync(
        Guid tenantPublicId,
        [FromBody] UpdateBody body,
        ISender sender, CancellationToken ct) =>
        await sender.Send(new UpdateTenantMfaPolicyCommand(
            tenantPublicId, body.IsRequired, body.MetodosAceptados), ct);

    /// <param name="MetodosAceptados">
    /// Literales: ["Totp"], ["WebAuthn"], o ambos. Omitirlo deja los metodos como
    /// estaban — NO los reinicia. Ver el comando para el porque.
    /// </param>
    public sealed record UpdateBody(bool IsRequired, IReadOnlyList<string>? MetodosAceptados = null);
}
