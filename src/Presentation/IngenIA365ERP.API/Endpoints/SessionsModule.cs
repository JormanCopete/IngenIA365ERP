using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Profile.SetDefaultTenant;
using IngenIA365ERP.Application.Identity.Sessions.GetMyActiveTenants;
using IngenIA365ERP.Application.Identity.Sessions.SelectTenant;
using IngenIA365ERP.Application.Identity.Sessions.SwitchTenant;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T084 — Endpoints de sesión (Feature 002, contracts/sessions.md):
/// active-tenants, select-tenant, switch-tenant + default-tenant management.
/// </summary>
public sealed class SessionsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Identity / Sessions")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/active-tenants", GetActiveTenantsAsync)
            .WithName("Sessions_GetActiveTenants");

        group.MapPost("/select-tenant", SelectTenantAsync)
            .WithName("Sessions_SelectTenant");

        group.MapPost("/switch-tenant", SwitchTenantAsync)
            .WithName("Sessions_SwitchTenant");

        // Default tenant — vive bajo /api/profile/* pero se gestiona aquí para
        // mantenerlo cerca del resto del flujo de sesión.
        var profileGroup = app.MapGroup("/api/profile")
            .WithTags("Identity / Profile")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        profileGroup.MapPut("/default-tenant", SetDefaultTenantAsync)
            .WithName("Profile_SetDefaultTenant");
    }

    private static async Task<object?> GetActiveTenantsAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new GetMyActiveTenantsQuery(), ct);

    private static async Task<object?> SelectTenantAsync(
        [FromBody] SelectTenantBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new SelectTenantCommand(body.TenantPublicId), ct);

    private static async Task<object?> SwitchTenantAsync(
        [FromBody] SwitchTenantBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new SwitchTenantCommand(body.TenantPublicId), ct);

    private static async Task<object?> SetDefaultTenantAsync(
        [FromBody] SetDefaultTenantBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new SetDefaultTenantCommand(body.TenantPublicId), ct);

    public sealed record SelectTenantBody(Guid TenantPublicId);
    public sealed record SwitchTenantBody(Guid TenantPublicId);
    public sealed record SetDefaultTenantBody(Guid? TenantPublicId);
}
