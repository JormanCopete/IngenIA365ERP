using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.ForceMfaReset;
using IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T115 — Endpoints SaaS-globales del master admin (US5).
/// Filter RequireMasterAdmin in-line + autorización delegada al handler.
/// </summary>
public sealed class SaasAdminModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var saasTenants = app.MapGroup("/api/saas/tenants")
            .WithTags("Identity / SaaS Admin")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        // Variante US5: crea tenant + envía invitación admin atomícamente.
        saasTenants.MapPost("/with-admin", RegisterWithAdminAsync)
            .WithName("Saas_RegisterTenantWithAdmin");

        // Force MFA reset (recuperación operativa).
        var saasUsers = app.MapGroup("/api/saas/users")
            .WithTags("Identity / SaaS Admin")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        saasUsers.MapPost("/{centralUserPublicId:guid}/force-mfa-reset", ForceMfaResetAsync)
            .WithName("Saas_ForceMfaReset");

        // Métodos de segundo factor que acepta la plataforma para el maestro.
        // No hay ruta para «si se exige»: eso es fijo, y hacerlo editable pondría
        // el interruptor de apagar la seguridad de la cuenta más poderosa al
        // alcance de quien consiga usarla una vez.
        var saasPolicy = app.MapGroup("/api/saas/mfa-policy")
            .WithTags("Identity / SaaS Admin")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        saasPolicy.MapGet("/", GetPlatformMfaPolicyAsync)
            .WithName("Saas_GetPlatformMfaPolicy");

        saasPolicy.MapPut("/", UpdatePlatformMfaPolicyAsync)
            .WithName("Saas_UpdatePlatformMfaPolicy");
    }

    // ---------- Política MFA de la plataforma ----------

    private static async Task<object?> GetPlatformMfaPolicyAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new Application.Saas.PlatformMfaPolicy.GetPlatformMfaPolicyQuery(), ct);

    private static async Task<object?> UpdatePlatformMfaPolicyAsync(
        [FromBody] PlatformMfaPolicyBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new Application.Saas.PlatformMfaPolicy.UpdatePlatformMfaPolicyCommand(
            body.MetodosAceptados ?? []), ct);

    /// <param name="MetodosAceptados">
    /// Literales: <c>["Totp"]</c>, <c>["WebAuthn"]</c>, o ambos. Lista vacía se
    /// rechaza — dejaría al maestro sin poder entrar y sin nadie que lo rescate.
    /// </param>
    public sealed record PlatformMfaPolicyBody(IReadOnlyList<string>? MetodosAceptados);

    // ---------- Register tenant with admin ----------

    private static async Task<object?> RegisterWithAdminAsync(
        [FromBody] RegisterTenantWithAdminBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct)
    {
        var guard = RequireMasterAdminAttribute.Check(http);
        if (!guard.IsAllowed)
            return Result.Failure<RegisterTenantWithAdminResult>(guard.ErrorCode!, guard.Message!);

        return await sender.Send(new RegisterTenantWithAdminCommand(
            body.Name, body.SchemaName, body.Subdomain, body.Nit, body.LegalName,
            body.LegalAddress, body.TaxRegime, body.ContactEmail, body.ContactPhone,
            body.PlanType, body.MaxUsers, body.StorageLimitMb, body.FirstAdminEmail), ct);
    }

    public sealed record RegisterTenantWithAdminBody(
        string Name, string SchemaName, string? Subdomain, string Nit,
        string LegalName, string? LegalAddress, string? TaxRegime,
        string ContactEmail, string? ContactPhone, string PlanType,
        int MaxUsers, long StorageLimitMb, string FirstAdminEmail);

    // ---------- Force MFA reset ----------

    private static async Task<object?> ForceMfaResetAsync(
        Guid centralUserPublicId,
        [FromBody] ForceMfaResetBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct)
    {
        var guard = RequireMasterAdminAttribute.Check(http);
        if (!guard.IsAllowed) return Result.Failure(guard.ErrorCode!, guard.Message!);

        return await sender.Send(new ForceMfaResetCommand(centralUserPublicId, body.Reason), ct);
    }

    public sealed record ForceMfaResetBody(string Reason);
}
