using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Sessions.SelectTenant;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Endpoints de sesión (Feature 002, contracts/sessions.md). Por ahora solo
/// <c>POST /api/sessions/select-tenant</c> — cuando se implemente
/// switch-tenant + default-tenant management se añaden aquí.
/// </summary>
public sealed class SessionsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Identity / Sessions")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapPost("/select-tenant", SelectTenantAsync)
            .WithName("Sessions_SelectTenant");
    }

    private static async Task<object?> SelectTenantAsync(
        [FromBody] SelectTenantBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new SelectTenantCommand(body.TenantPublicId), ct);

    public sealed record SelectTenantBody(Guid TenantPublicId);
}
