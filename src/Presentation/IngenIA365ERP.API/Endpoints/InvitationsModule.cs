using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Invitations.AcceptInvitation;
using IngenIA365ERP.Application.Invitations.IssueMasterInvitation;
using IngenIA365ERP.Application.Invitations.IssueTenantInvitation;
using IngenIA365ERP.Application.Invitations.PreviewInvitation;
using IngenIA365ERP.Application.Invitations.RevokeInvitation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using IngenIA365ERP.Application.Invitations.GestionInvitaciones;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T058 — Endpoints de invitación para Feature 002 (US1 — Onboarding).
/// Cinco rutas:
/// <list type="bullet">
///   <item><c>POST /api/tenants/{tenantPublicId}/invitations</c> — emite desde tenant admin.</item>
///   <item><c>POST /api/saas/invitations</c> — emite desde master admin (puede asignar IsTenantAdmin).</item>
///   <item><c>GET  /api/invitations/{token}/preview</c> — pública, lee sin consumir.</item>
///   <item><c>POST /api/invitations/accept</c> — pública (3 ramas XOR).</item>
///   <item><c>DELETE /api/invitations/{publicId}</c> — autorización delegada al handler.</item>
/// </list>
///
/// <para>
/// Las rutas anonymous (<c>preview</c>, <c>accept</c>) están exentas de la
/// resolución de tenant (<c>TenantResolutionMiddleware</c> T044 las salta por
/// el prefijo <c>/api/invitations/</c>).
/// </para>
/// </summary>
public sealed class InvitationsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ---------- Tenant admin emite ----------
        var tenantScoped = app
            .MapGroup("/api/tenants/{tenantPublicId:guid}/invitations")
            .WithTags("Identity / Invitations")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        tenantScoped.MapPost("/", IssueByTenantAdminAsync)
            .WithName("Invitations_IssueByTenantAdmin");

        // Faltaba poder VER las invitaciones emitidas: se podia crear una y
        // revocarla conociendo su id, pero no consultar cuales existian ni en
        // que estado estaban. Si el correo no llegaba, no quedaba rastro.
        tenantScoped.MapGet("/", ListarAsync)
            .WithName("Invitations_Listar");

        tenantScoped.MapPost("/{invitacionPublicId:guid}/reenviar", ReenviarAsync)
            .WithName("Invitations_Reenviar");

        // ---------- Master admin emite ----------
        var saas = app
            .MapGroup("/api/saas/invitations")
            .WithTags("Identity / Invitations (SaaS)")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        saas.MapPost("/", IssueByMasterAsync)
            .WithName("Invitations_IssueByMaster");

        // ---------- Público (anónimo) + autorización delegada ----------
        var anon = app
            .MapGroup("/api/invitations")
            .WithTags("Identity / Invitations")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        anon.MapGet("/{token}/preview", PreviewAsync)
            .WithName("Invitations_Preview")
            .AllowAnonymous();

        anon.MapPost("/accept", AcceptAsync)
            .WithName("Invitations_Accept")
            .AllowAnonymous();

        anon.MapDelete("/{publicId:guid}", RevokeAsync)
            .WithName("Invitations_Revoke")
            .RequireAuthorization();
    }

    // ---------- Listar / reenviar ----------

    private static async Task<object?> ListarAsync(
        Guid tenantPublicId,
        [FromQuery] bool incluirCerradas,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ListarInvitacionesQuery(tenantPublicId, incluirCerradas), ct);

    private static async Task<object?> ReenviarAsync(
        Guid tenantPublicId,
        Guid invitacionPublicId,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ReenviarInvitacionCommand(invitacionPublicId), ct);

    // ---------- Issue (tenant admin) ----------

    private static async Task<object?> IssueByTenantAdminAsync(
        Guid tenantPublicId,
        [FromBody] IssueByTenantAdminBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct)
    {
        var guard = RequireTenantAdminAttribute.Check(http, tenantPublicId.ToString());
        if (!guard.IsAllowed)
        {
            return Result.Failure<IssueTenantInvitationResult>(guard.ErrorCode!, guard.Message!);
        }

        return await sender.Send(new IssueTenantInvitationCommand(
            TenantPublicId: tenantPublicId,
            Email: body.Email), ct);
    }

    public sealed record IssueByTenantAdminBody(string Email);

    // ---------- Issue (master) ----------

    private static async Task<object?> IssueByMasterAsync(
        [FromBody] IssueByMasterBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct)
    {
        var guard = RequireMasterAdminAttribute.Check(http);
        if (!guard.IsAllowed)
        {
            return Result.Failure<IssueMasterInvitationResult>(guard.ErrorCode!, guard.Message!);
        }

        return await sender.Send(new IssueMasterInvitationCommand(
            TenantPublicId: body.TenantPublicId,
            Email: body.Email,
            InviteAsTenantAdmin: body.InviteAsTenantAdmin), ct);
    }

    public sealed record IssueByMasterBody(
        Guid TenantPublicId,
        string Email,
        bool InviteAsTenantAdmin);

    // ---------- Preview ----------

    private static async Task<object?> PreviewAsync(
        string token,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new PreviewInvitationQuery(token), ct);

    // ---------- Accept ----------

    private static async Task<object?> AcceptAsync(
        [FromBody] AcceptBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new AcceptInvitationCommand(
            Token: body.Token,
            Registration: body.Registration,
            ExistingCredentials: body.ExistingCredentials,
            UseActiveSession: body.UseActiveSession), ct);

    public sealed record AcceptBody(
        string Token,
        NewRegistrationInput? Registration = null,
        ExistingCredentialsInput? ExistingCredentials = null,
        bool UseActiveSession = false);

    // ---------- Revoke ----------

    private static async Task<object?> RevokeAsync(
        Guid publicId,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RevokeInvitationCommand(publicId), ct);
}
