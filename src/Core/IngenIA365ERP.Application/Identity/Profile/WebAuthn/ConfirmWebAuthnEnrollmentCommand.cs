using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.WebAuthn;

/// <summary>
/// Segundo viaje del alta: llega la respuesta firmada del autenticador y, si
/// verifica, la llave queda inscrita.
/// </summary>
/// <param name="Label">Nombre del dispositivo. Opcional.</param>
public sealed record ConfirmWebAuthnEnrollmentCommand(
    string RetoId, string RespuestaJson, string? Label = null)
    : IRequest<Result<ConfirmWebAuthnEnrollmentResult>>;

/// <param name="CodigosDeRecuperacion">
/// Sólo vienen si era su PRIMERA credencial. Con la segunda la lista va vacía:
/// emitirlos invalidaría los que ya guardó.
/// </param>
public sealed record ConfirmWebAuthnEnrollmentResult(
    Guid CredencialPublicId,
    IReadOnlyList<string> CodigosDeRecuperacion);

public sealed class ConfirmWebAuthnEnrollmentCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    IWebAuthnChallengeStore retos,
    ITenantMembershipReader memberships,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ConfirmWebAuthnEnrollmentCommandHandler> logger)
    : IRequestHandler<ConfirmWebAuthnEnrollmentCommand, Result<ConfirmWebAuthnEnrollmentResult>>
{
    public async Task<Result<ConfirmWebAuthnEnrollmentResult>> Handle(
        ConfirmWebAuthnEnrollmentCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<ConfirmWebAuthnEnrollmentResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        }

        if (currentUser.Purpose != CentralJwtPurposes.Full
            && currentUser.Purpose != CentralJwtPurposes.MfaEnroll)
        {
            return Result.Failure<ConfirmWebAuthnEnrollmentResult>(
                "Identity.WrongTokenPurpose",
                "Este endpoint requiere purpose=full o mfa-enroll.");
        }

        var centralUserId = currentUser.CentralUserId.Value;

        // El reto se consume: de un solo uso. Si no está, o es de otra persona, o
        // se emitió para entrar en vez de para inscribir, no aparece.
        var opcionesOriginales = await retos.ConsumirAsync(
            centralUserId, PropositoDeReto.Alta, request.RetoId, ct);

        if (opcionesOriginales is null)
        {
            return Result.Failure<ConfirmWebAuthnEnrollmentResult>(
                "Profile.Mfa.RetoVencido",
                "El proceso caducó o ya se usó. Volvé a empezar desde «agregar autenticador».");
        }

        var verificacion = await webAuthn.VerificarAltaAsync(
            request.RespuestaJson,
            opcionesOriginales,
            credenciales.ElCredentialIdEstaLibreAsync,
            ct);

        if (!verificacion.Exito)
        {
            return Result.Failure<ConfirmWebAuthnEnrollmentResult>(
                "Profile.Mfa.WebAuthnInvalido",
                verificacion.Error ?? "No se pudo verificar la llave.");
        }

        // ¿Es su primera credencial? Hay que saberlo ANTES de inscribir la nueva.
        var esLaPrimera = await credenciales.ContarActivasAsync(centralUserId, ct) == 0;

        var ahora = clock.UtcNow;

        // La credencial PRIMERO, la bandera DESPUÉS. Si algo falla en medio, la
        // persona queda con llave y sin bandera —el login no le pide segundo
        // factor y entra— y nunca al revés, que sería pedirle una llave que el
        // sistema no reconoce.
        var publicId = await credenciales.InscribirWebAuthnAsync(
            new NuevaCredencialWebAuthn(
                centralUserId,
                verificacion.CredentialId!,
                verificacion.ClavePublicaCose!,
                verificacion.ContadorDeFirmas,
                verificacion.AaGuid,
                verificacion.TransportsJson,
                verificacion.EsRespaldable,
                verificacion.EstaRespaldada,
                verificacion.FormatoDeAtestacion,
                request.Label),
            ahora, ct);

        // Sin esto, quien sólo tuviera una passkey entraría SIN segundo factor: el
        // login mira esa bandera, y con ella apagada ni siquiera lo pide.
        var codigos = await centralIdentity.ActivarSegundoFactorAsync(centralUserId, esLaPrimera, ct);

        await memberships.InvalidateLocalCacheAsync(centralUserId, ct);
        await EmitirAuditoriaAsync(centralUserId, publicId, ahora, ct);

        return Result.Success(new ConfirmWebAuthnEnrollmentResult(publicId, codigos));
    }

    private async Task EmitirAuditoriaAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime ahora, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.ProfileMfaEnrolled,
                EntityType: nameof(Domain.Entities.Admin.WebAuthnCredential),
                EntityPublicId: credencialPublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/profile/mfa/webauthn/confirm",
                HttpMethod: "POST",
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: ahora), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló al inscribir una passkey.");
        }
    }
}
