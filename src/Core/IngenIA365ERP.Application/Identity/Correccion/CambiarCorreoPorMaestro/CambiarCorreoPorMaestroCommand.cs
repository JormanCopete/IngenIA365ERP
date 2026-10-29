using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Correccion.CambiarCorreoPorMaestro;

/// <summary>
/// Feature 017 (US7, T079, FR-039): el rescate. La persona perdió el buzón y no puede cambiar el correo ella
/// misma (eso exige clave, segundo factor y un código al correo nuevo). Lo cambia soporte de la plataforma con
/// motivo; avisa a los dos correos —el anterior se entera aunque ya no lo use—, rota el sello y queda el evento
/// <c>Identity.Email.ChangedByMaster</c> y una fila <c>ByMaster</c> en <c>ADM_EmailChangeRequests</c>. El
/// administrador de la cooperativa no tiene esta ruta: no cambia correos (D-12).
/// </summary>
public sealed record CambiarCorreoPorMaestroCommand(Guid CentralUserId, string? NewEmail, string? Reason)
    : IRequest<Result<CorreoCambiadoResult>>;

public sealed record CorreoCambiadoResult(Guid CentralUserId, string Email);

public sealed class CambiarCorreoPorMaestroCommandValidator : AbstractValidator<CambiarCorreoPorMaestroCommand>
{
    public CambiarCorreoPorMaestroCommandValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().WithMessage("Correo no válido.").MaximumLength(256);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(500);
    }
}

public sealed class CambiarCorreoPorMaestroCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext db,
    PropagadorDeIdentidad propagador,
    CorreosDeIdentidad correos,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<CambiarCorreoPorMaestroCommand, Result<CorreoCambiadoResult>>
{
    public async Task<Result<CorreoCambiadoResult>> Handle(CambiarCorreoPorMaestroCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.Purpose != CentralJwtPurposes.Full || !currentUser.IsGlobalMasterAdmin
            || currentUser.CentralUserId is not { } actor)
            return Result.Failure<CorreoCambiadoResult>("Saas.MasterOnly", "Sólo el maestro de plataforma.");

        var cuenta = await centralIdentity.FindByIdAsync(request.CentralUserId, ct);
        if (cuenta is null)
            return Result.Failure<CorreoCambiadoResult>("Identity.NotFound", "Identidad no encontrada.");

        var nuevo = request.NewEmail!.Trim();
        var anterior = cuenta.Email;
        if (string.Equals(nuevo, anterior, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CorreoCambiadoResult>("Identity.Email.Unchanged", "Ese ya es el correo de la cuenta.");

        var now = clock.UtcNow;
        var cambio = await centralIdentity.ChangeEmailAsync(cuenta.Id, nuevo, now, ct);
        if (!cambio.Succeeded)
            return cambio.ErrorCodes.Contains("Identity.DuplicateEmail")
                ? Result.Failure<CorreoCambiadoResult>("Identity.Email.Taken", "Ese correo ya lo usa otra cuenta.")
                : Result.Failure<CorreoCambiadoResult>(cambio.ErrorCodes.FirstOrDefault() ?? "Identity.Email.Invalid", "No se pudo cambiar el correo.");

        db.EmailChangeRequests.Add(EmailChangeRequest.PorMaestro(cuenta.Id, nuevo, nuevo.ToUpperInvariant(), request.Reason!.Trim(), now));
        await db.SaveChangesAsync(ct);
        await propagador.CorreoAsync(cuenta.Id, nuevo, ct);

        await auditoria.GlobalAsync(
            AuditEventTypes.IdentityEmailChangedByMaster, actor, currentUser.Email, nameof(CentralUser), cuenta.Id,
            new { Email = anterior }, new { Email = nuevo, request.Reason }, now, ct);

        var modelo = new Dictionary<string, string?>
        {
            ["Nombre"] = cuenta.NombreParaMostrar,
            ["Quien"] = "Soporte de la plataforma",
            ["CorreoNuevo"] = nuevo,
        };
        await correos.EnviarAsync(anterior, "EmailChangedNoticeEmail", "El correo de tu cuenta cambió", modelo, ct);
        await correos.EnviarAsync(nuevo, "EmailChangedNoticeEmail", "El correo de tu cuenta cambió", modelo, ct);

        return Result.Success(new CorreoCambiadoResult(cuenta.Id, nuevo));
    }
}
