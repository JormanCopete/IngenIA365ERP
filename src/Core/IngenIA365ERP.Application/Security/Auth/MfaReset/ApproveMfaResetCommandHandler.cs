using IngenIA365ERP.Application.Common.Interfaces;
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
/// - Al acumular dos aprobaciones distintas, se ejecuta el reset: limpia
///   <c>MfaSecret</c>, soft-deletea backup codes y notifica al usuario.
/// - Si pasaron más de 24h desde la creación, la solicitud queda <c>Expired</c>.
/// </summary>
public sealed class ApproveMfaResetCommandHandler : IRequestHandler<ApproveMfaResetCommand, Result<MfaResetApprovalResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISender _mediator;

    public ApproveMfaResetCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        ISender mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _mediator = mediator;
    }

    public async Task<Result<MfaResetApprovalResult>> Handle(ApproveMfaResetCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<MfaResetApprovalResult>("Generic.Unauthorized", "No autenticado.");
        }

        var approverId = _currentUser.UserId.Value;

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
        entry.SecondApproverId = approverId;
        entry.SecondApprovalAt = now;
        entry.Status = MfaResetStatus.Approved;

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId, ct);
        if (target is null)
        {
            return Result.Failure<MfaResetApprovalResult>(
                "Generic.NotFound",
                "El usuario objetivo del reset ya no existe.");
        }

        target.MfaSecret = null;
        target.IsMfaEnabled = false;

        var backupCodes = await _db.MfaBackupCodes
            .Where(c => c.UserId == target.Id && c.UsedAt == null)
            .ToListAsync(ct);
        foreach (var bc in backupCodes)
        {
            bc.IsDeleted = true;
            bc.DeletedAt = now;
            bc.DeletedBy = _currentUser.UserName;
        }

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
