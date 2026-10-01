using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.RequestPresenceChallenge;

/// <summary>
/// Pide el desafío de una aprobación presencial (feature 012, T33, T085; contracts/api.md §15.2,
/// <c>POST /api/inventory/approvals/{id}/presence-challenge</c>): el aprobador está junto al equipo de quien pidió (el
/// supervisor en la caja) y se identifica con <b>sus</b> credenciales —passkey o TOTP—, nunca con contraseña. (nuevo)
/// </summary>
public sealed record RequestPresenceChallengeCommand(Guid RequestPublicId, string ApproverEmail)
    : IRequest<Result<DesafioDePresenciaDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestPresenceChallengeCommandValidator : AbstractValidator<RequestPresenceChallengeCommand>
{
    public RequestPresenceChallengeCommandValidator()
    {
        RuleFor(x => x.ApproverEmail).NotEmpty().MaximumLength(256).EmailAddress().WithMessage("Indicá el correo del aprobador.");
    }
}

/// <summary>
/// Sólo desde la sesión de quien pidió (<c>Approvals.Presence.NotRequester</c>) y sobre una solicitud pendiente. El
/// aprobador es una persona activa de la cooperativa con identidad central y algún método: si no, el mismo
/// <c>Approvals.Presence.Invalid</c> (422, nunca 401) sin decir cuál falló. El desafío dura dos minutos y vive en la
/// ranura de caché de la cooperativa.
/// </summary>
public sealed class RequestPresenceChallengeCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    IDesafiosDePresencia desafios,
    IDateTimeService reloj)
    : IRequestHandler<RequestPresenceChallengeCommand, Result<DesafioDePresenciaDto>>
{
    /// <summary>Lo que dura un desafío presencial.</summary>
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

    public async Task<Result<DesafioDePresenciaDto>> Handle(RequestPresenceChallengeCommand request, CancellationToken ct)
    {
        var solicitud = await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RequestPublicId, ct);
        if (solicitud is null) return Result.Failure<DesafioDePresenciaDto>(ErroresDeAprobaciones.SolicitudInexistente());

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId != solicitud.RequestedByUserId)
            return Result.Failure<DesafioDePresenciaDto>(ErroresDeAprobaciones.PresenciaNoSolicitante());

        if (solicitud.Status != ApprovalRequestStatus.Pending)
            return Result.Failure<DesafioDePresenciaDto>(ErroresDeAprobaciones.SolicitudNoPendiente(solicitud.Status));

        var correo = request.ApproverEmail.Trim();
        var aprobador = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.CentralUserId != null && (u.Email == correo || u.Username == correo))
            .Select(u => new { u.Id, CentralUserId = u.CentralUserId!.Value, Nombre = u.Email ?? u.Username })
            .FirstOrDefaultAsync(ct);
        if (aprobador is null) return Result.Failure<DesafioDePresenciaDto>(ErroresDeAprobaciones.PresenciaInvalida());

        var llaves = await credenciales.ListarWebAuthnActivasAsync(aprobador.CentralUserId, ct);
        var totps = await credenciales.ListarCifradosTotpActivosAsync(aprobador.CentralUserId, ct);
        var metodos = new List<string>();
        if (llaves.Count > 0) metodos.Add("Passkey");
        if (totps.Count > 0) metodos.Add("Totp");
        if (metodos.Count == 0) return Result.Failure<DesafioDePresenciaDto>(ErroresDeAprobaciones.PresenciaInvalida());

        var opciones = llaves.Count > 0 ? webAuthn.CrearOpcionesDeIngreso(llaves) : null;
        var vence = reloj.UtcNow.Add(Vigencia);
        var desafio = new DesafioDePresencia(Guid.NewGuid(), solicitud.PublicId, solicitud.RequestedByUserId, aprobador.Id,
            aprobador.CentralUserId, aprobador.Nombre, metodos, opciones?.ParaGuardarJson, vence);
        await desafios.GuardarAsync(desafio, Vigencia, ct);

        return Result.Success(new DesafioDePresenciaDto(desafio.PublicId, vence, metodos, opciones?.OpcionesJson));
    }
}
