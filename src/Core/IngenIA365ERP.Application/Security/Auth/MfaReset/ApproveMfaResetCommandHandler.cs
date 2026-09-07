using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

/// <summary>
/// Aprueba una solicitud de reset MFA. Reglas (FR-013):
/// - El solicitante no puede aprobarse a sí mismo.
/// - Un mismo aprobador no puede contar como dos aprobaciones.
/// - Al acumular dos aprobaciones distintas, se ejecuta el reset sobre la
///   identidad central y se notifica al usuario.
/// - Si pasaron más de 24h desde la creación, la solicitud queda <c>Expired</c>.
///
/// El reset cae sobre <c>ADM_CentralUsers</c> porque es ahí donde el login
/// verifica el segundo factor. Antes caía sobre <c>SEC_Users</c> —el modelo de
/// Fase 0, que ya nadie lee ni escribe—, así que dos administradores aprobaban,
/// salía el correo diciendo que el segundo factor se había restablecido, y la
/// persona seguía bloqueada fuera de su cuenta.
/// </summary>
public sealed class ApproveMfaResetCommandHandler : IRequestHandler<ApproveMfaResetCommand, Result<MfaResetApprovalResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentCentralUserContext _usuarioCentral;
    private readonly IDateTimeService _clock;
    private readonly ISender _mediator;
    private readonly ICentralIdentityProvider _identidadCentral;

    public ApproveMfaResetCommandHandler(
        IApplicationDbContext db,
        ICurrentCentralUserContext usuarioCentral,
        IDateTimeService clock,
        ISender mediator,
        ICentralIdentityProvider identidadCentral)
    {
        _db = db;
        _usuarioCentral = usuarioCentral;
        _clock = clock;
        _mediator = mediator;
        _identidadCentral = identidadCentral;
    }

    public async Task<Result<MfaResetApprovalResult>> Handle(ApproveMfaResetCommand request, CancellationToken ct)
    {
        var quienAprueba = await QuienLlamaEnLaCooperativa.IdAsync(_db, _usuarioCentral, ct);
        if (quienAprueba is null)
        {
            return Result.Failure<MfaResetApprovalResult>("Generic.Unauthorized", "No autenticado.");
        }

        var approverId = quienAprueba.Value;

        var entry = await _db.MfaResetRequests
            .FirstOrDefaultAsync(r => r.PublicId == request.RequestPublicId, ct);

        if (entry is null)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Generic.NotFound",
                "La solicitud de reset MFA no existe.");
        }

        if (entry.ExpiresAt < _clock.UtcNow && entry.Status == MfaResetStatus.Pending)
        {
            entry.Status = MfaResetStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return Result.Failure<MfaResetApprovalResult>(
                "Auth.MfaResetExpired",
                "La solicitud expiró. Crea una nueva si aún se requiere el reset.");
        }

        if (entry.Status is MfaResetStatus.Executed or MfaResetStatus.Rejected or MfaResetStatus.Expired)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Auth.MfaResetAlreadyClosed",
                $"La solicitud ya está {entry.Status}.");
        }

        if (entry.RequestedBy == approverId)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Auth.MfaResetCannotApproveOwnRequest",
                "El solicitante no puede aprobar su propia solicitud.");
        }

        if (entry.FirstApproverId == approverId || entry.SecondApproverId == approverId)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Auth.MfaResetAlreadyApprovedBySameUser",
                "Ya aprobaste esta solicitud anteriormente.");
        }

        var now = _clock.UtcNow;

        if (entry.FirstApproverId is null)
        {
            entry.FirstApproverId = approverId;
            entry.FirstApprovalAt = now;
            await _db.SaveChangesAsync(ct);
            return Result.Success(new MfaResetApprovalResult("Approved"));
        }

        // Segunda aprobación → ejecuta.
        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId, ct);
        if (target is null)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Generic.NotFound",
                "El usuario objetivo del reset ya no existe.");
        }

        // Sin puente a la identidad central no hay segundo factor que
        // restablecer, y hay que decirlo. Devolver "Executed" sin haber
        // ejecutado es el fallo silencioso que prohíbe el Principio IX, y es
        // exactamente lo que este handler hacía.
        if (target.CentralUserId is null)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Auth.MfaResetSinIdentidadCentral",
                "El usuario no está enlazado con la identidad central, así que su segundo factor no se puede restablecer por esta vía. Escalá al administrador maestro.");
        }

        // Primero el efecto real; después el registro. Si esto lanza, la
        // solicitud queda Pending y se puede reintentar, en vez de quedar
        // marcada como ejecutada sin haberlo sido.
        await _identidadCentral.ResetMfaAsync(target.CentralUserId.Value, ct);

        entry.SecondApproverId = approverId;
        entry.SecondApprovalAt = now;

        // Aquí ya no se toca SEC_Users.MfaSecret / IsMfaEnabled ni la tabla
        // SEC_MfaBackupCodes. Son residuo de Fase 0: nadie los lee, y limpiarlos
        // sólo servía para que el diff pareciera hacer algo. El efecto real es
        // el ResetMfaAsync de arriba.
        //
        // Las columnas y la tabla SIGUEN mapeadas en EF a propósito. Sacarlas del
        // modelo hace que el próximo `migrations add` —cualquiera, para cualquier
        // fin— genere DropTable + DropColumn contra la base de CADA cooperativa,
        // sin marcarse como destructiva. El Principio XII pide backup y segundo
        // revisor para eso, así que va en su propia pasada.
        entry.Status = MfaResetStatus.Executed;
        entry.ExecutedAt = now;

        await _db.SaveChangesAsync(ct);

        await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
            RecipientUserPublicId: target.PublicId,
            Type: NotificationType.MfaReset,
            Subject: "Tu segundo factor fue restablecido",
            Body: "Dos administradores autorizaron el reset de tu MFA. Inscríbete nuevamente desde tu perfil de seguridad.",
            Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);

        return Result.Success(new MfaResetApprovalResult("Executed"));
    }
}
