using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.DecideApproval;

/// <summary>La prueba de presencia del aprobador (contracts/api.md §15.2). (nuevo)</summary>
public sealed record PresenciaDto(Guid ChallengePublicId, string? Assertion, string? TotpCode);

/// <summary>
/// Decide el nivel actual de una solicitud (feature 012, T33, T085; contracts/api.md §15.2,
/// <c>POST /api/inventory/approvals/{id}/decide</c>). <see cref="ApprovalMethod.OwnSession"/> decide quien tiene la
/// sesión; <see cref="ApprovalMethod.InPersonPasskey"/> e <see cref="ApprovalMethod.InPersonTotp"/>, el aprobador
/// presente en el equipo de quien pidió, con el desafío de <c>presence-challenge</c>.
/// </summary>
public sealed record DecideApprovalCommand(
    Guid RequestPublicId,
    ApprovalDecisionKind Decision,
    string? Reason,
    ApprovalMethod Method,
    PresenciaDto? Presence,
    string ExpectedContentSha256)
    : IRequest<Result<DecisionResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    /// <summary>El motivo (obligatorio al rechazar) va a la auditoría como el de toda operación con motivo (T420, US12-4).</summary>
    string IConMotivo.Reason => Reason ?? string.Empty;
}

/// <summary>Rechazar exige motivo (400 si falta); lo presencial exige el desafío y la prueba de su método.</summary>
public sealed class DecideApprovalCommandValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalCommandValidator()
    {
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.ExpectedContentSha256).NotEmpty().WithMessage("Indicá la huella de lo que viste (expectedContentSha256).");
        RuleFor(x => x.Reason)
            .Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("Indicá el motivo del rechazo.")
            .When(x => x.Decision == ApprovalDecisionKind.Reject);
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("El motivo admite hasta 500 caracteres.");
        RuleFor(x => x.Presence).NotNull().WithMessage("La aprobación presencial lleva el desafío (presence).")
            .When(x => x.Method != ApprovalMethod.OwnSession);
        RuleFor(x => x.Presence!.Assertion).NotEmpty().WithMessage("Falta la aserción de la llave del aprobador.")
            .When(x => x.Method == ApprovalMethod.InPersonPasskey && x.Presence is not null);
        RuleFor(x => x.Presence!.TotpCode).NotEmpty().WithMessage("Falta el código del aprobador.")
            .When(x => x.Method == ApprovalMethod.InPersonTotp && x.Presence is not null);
    }
}

/// <summary>
/// Identifica al aprobador presente, si lo hay, y deja la decisión al motor. Lo presencial se hace sólo desde la sesión
/// del solicitante (<c>NotRequester</c>); el desafío se consume en el primer intento (<c>Expired</c> si venció o no
/// existe); la passkey se verifica contra las opciones que se le presentaron y debe ser del aprobador; el TOTP es de
/// un solo uso (<c>TotpReused</c>). Toda falla de identidad es <c>Approvals.Presence.Invalid</c>: 422, nunca 401.
/// </summary>
public sealed class DecideApprovalCommandHandler(
    IMotorDeAprobaciones motor,
    IApplicationDbContext db,
    IActorActual actorActual,
    IDesafiosDePresencia desafios,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    ICentralIdentityProvider identidadCentral,
    IDateTimeService reloj)
    : IRequestHandler<DecideApprovalCommand, Result<DecisionResultDto>>
{
    /// <summary>Cuánto se recuerda un código TOTP usado: más que su ventana de validez.</summary>
    private static readonly TimeSpan MemoriaDelTotp = TimeSpan.FromMinutes(3);

    public async Task<Result<DecisionResultDto>> Handle(DecideApprovalCommand request, CancellationToken ct)
    {
        AprobadorPresente? presente = null;
        if (request.Method != ApprovalMethod.OwnSession)
        {
            var identificado = await IdentificarAsync(request, ct);
            if (identificado.IsFailure) return Result.Failure<DecisionResultDto>(identificado.Error);
            presente = identificado.Value;
        }

        return await motor.DecidirAsync(
            new DecisionDeAprobacion(request.RequestPublicId, request.Decision, request.Reason, request.ExpectedContentSha256, presente), ct);
    }

    private async Task<Result<AprobadorPresente>> IdentificarAsync(DecideApprovalCommand request, CancellationToken ct)
    {
        var solicitud = await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RequestPublicId, ct);
        if (solicitud is null) return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.SolicitudInexistente());

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId != solicitud.RequestedByUserId)
            return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaNoSolicitante());

        var desafio = await desafios.ConsumirAsync(request.Presence!.ChallengePublicId, ct);
        if (desafio is null || desafio.ExpiresAt < reloj.UtcNow)
            return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaVencida());
        if (desafio.RequestPublicId != solicitud.PublicId || desafio.RequesterUserId != solicitud.RequestedByUserId)
            return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaInvalida());

        if (request.Method == ApprovalMethod.InPersonTotp)
        {
            var codigo = request.Presence.TotpCode!.Trim();
            if (!desafio.Methods.Contains("Totp") || !await identidadCentral.VerifyMfaCodeAsync(desafio.ApproverCentralUserId, codigo, ct))
                return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaInvalida());
            if (!await desafios.MarcarTotpUsadoAsync(desafio.ApproverCentralUserId, codigo, MemoriaDelTotp, ct))
                return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.TotpReusado());
            return Result.Success(new AprobadorPresente(desafio.ApproverUserId, desafio.ApproverName, ApprovalMethod.InPersonTotp, null));
        }

        if (desafio.OpcionesWebAuthnJson is null || LeerCredentialId(request.Presence.Assertion!) is not { } credentialId)
            return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaInvalida());

        // Que la llave exista Y sea del aprobador: una llave ajena válida firmaría bien, pero de otra persona.
        var guardada = await credenciales.BuscarWebAuthnPorCredentialIdAsync(credentialId, ct);
        if (guardada is null || guardada.CentralUserId != desafio.ApproverCentralUserId)
            return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaInvalida());

        var verificacion = await webAuthn.VerificarIngresoAsync(request.Presence.Assertion!, desafio.OpcionesWebAuthnJson,
            guardada.ClavePublicaCose, guardada.SignCount, ct);
        if (!verificacion.Exito) return Result.Failure<AprobadorPresente>(ErroresDeAprobaciones.PresenciaInvalida());

        await credenciales.ActualizarContadorWebAuthnAsync(guardada.PublicId, verificacion.ContadorNuevo, verificacion.EstaRespaldada, ct);
        await credenciales.MarcarUsoAsync(desafio.ApproverCentralUserId, guardada.PublicId, reloj.UtcNow, ct);
        return Result.Success(new AprobadorPresente(desafio.ApproverUserId, desafio.ApproverName, ApprovalMethod.InPersonPasskey, guardada.PublicId));
    }

    /// <summary>El <c>rawId</c> (base64url) de la aserción, como lo lee el ingreso con passkey.</summary>
    private static byte[]? LeerCredentialId(string asercion)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(asercion);
            if (!doc.RootElement.TryGetProperty("rawId", out var raw) || raw.GetString() is not { Length: > 0 } texto) return null;
            return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(texto);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }
}
