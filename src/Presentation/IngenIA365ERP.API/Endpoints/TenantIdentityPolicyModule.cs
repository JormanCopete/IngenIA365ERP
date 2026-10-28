using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Politica;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Feature 017 — GET/PUT /api/tenants/{publicId}/identity-policy: tipos de documento aceptados,
/// fecha desde la cual se exige el documento e intentos al activar una invitación. Mismo guardián que
/// la política de segundo factor: administrador de la cooperativa o maestro.
/// </summary>
public sealed class TenantIdentityPolicyModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantPublicId:guid}/identity-policy")
            .WithTags("Identity / Tenant Identity Policy")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", GetAsync).WithName("TenantIdentityPolicy_Get");
        group.MapPut("/", UpdateAsync).WithName("TenantIdentityPolicy_Update");
    }

    private static async Task<object?> GetAsync(Guid tenantPublicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new GetPoliticaDeIdentidadQuery(tenantPublicId), ct);

    private static async Task<object?> UpdateAsync(
        Guid tenantPublicId, [FromBody] UpdateBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new UpdatePoliticaDeIdentidadCommand(
            tenantPublicId, body.TiposAceptados ?? [], body.DocumentoExigidoDesde, body.IntentosAlActivar), ct);

    public sealed record UpdateBody(
        IReadOnlyList<string>? TiposAceptados,
        DateOnly? DocumentoExigidoDesde,
        int IntentosAlActivar);
}
