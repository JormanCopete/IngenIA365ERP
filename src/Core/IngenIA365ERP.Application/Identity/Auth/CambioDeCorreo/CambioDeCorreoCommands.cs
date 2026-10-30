using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.Auth.CambioDeCorreo;

/// <summary>
/// Feature 017 (US7, T077, FR-037/FR-038): la persona cambia su correo en dos pasos. Pedirlo exige el token
/// <c>step-up</c> (clave y segundo factor hace menos de cinco minutos) y manda un código de seis dígitos al
/// correo nuevo; confirmarlo exige la sesión y el código —hasheado en la base, 30 minutos, 5 intentos—. Al
/// confirmar cambian correo y nombre de usuario de Identity, el correo queda verificado, se rota el sello
/// (las demás sesiones mueren en su próxima renovación) y se avisa al correo anterior. Nadie más cambia el
/// correo de una persona salvo el rescate del maestro.
/// </summary>
public sealed record SolicitarCambioDeCorreoCommand(string? NewEmail, string? IpAddress = null)
    : IRequest<Result<CambioDeCorreoSolicitado>>;

public sealed record CambioDeCorreoSolicitado(int ExpiresInSeconds);

public sealed record ConfirmarCambioDeCorreoCommand(string? Code) : IRequest<Result<CorreoConfirmado>>;

public sealed record CorreoConfirmado(string Email);

public sealed class SolicitarCambioDeCorreoCommandValidator : AbstractValidator<SolicitarCambioDeCorreoCommand>
{
    public SolicitarCambioDeCorreoCommandValidator() =>
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().WithMessage("Correo no válido.").MaximumLength(256);
}

public sealed class ConfirmarCambioDeCorreoCommandValidator : AbstractValidator<ConfirmarCambioDeCorreoCommand>
{
    public ConfirmarCambioDeCorreoCommandValidator() =>
        RuleFor(x => x.Code).NotEmpty().WithMessage("Digite el código que llegó al correo nuevo.").MaximumLength(12);
}

/// <summary>El código de seis dígitos y su huella. La huella mezcla la cuenta para que dos personas con el
/// mismo código no tengan la misma fila.</summary>
public static class CodigoDeCambioDeCorreo
{
    public static string Nuevo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static byte[] Huella(Guid cuenta, string codigo) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{cuenta:N}:{codigo.Trim()}"));
}

public sealed class SolicitarCambioDeCorreoCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext db,
    CorreosDeIdentidad correos,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<SolicitarCambioDeCorreoCommand, Result<CambioDeCorreoSolicitado>>
{
    public async Task<Result<CambioDeCorreoSolicitado>> Handle(SolicitarCambioDeCorreoCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is not { } id)
            return Result.Failure<CambioDeCorreoSolicitado>("Identity.Unauthenticated", "Inicie sesión de nuevo.");
        if (currentUser.Purpose != CentralJwtPurposes.StepUp)
            return Result.Failure<CambioDeCorreoSolicitado>(
                "Identity.StepUpRequired", "Confirme su contraseña y su segundo factor antes de cambiar el correo.");

        var persona = await centralIdentity.FindByIdAsync(id, ct);
        if (persona is null)
            return Result.Failure<CambioDeCorreoSolicitado>("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var nuevo = request.NewEmail!.Trim();
        if (string.Equals(nuevo, persona.Email, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CambioDeCorreoSolicitado>("Identity.Email.Unchanged", "Ese ya es su correo.");
        // Aviso temprano con el resolvedor (correo verificado de otra cuenta). Uno sin verificar lo detecta el
        // proveedor al confirmar, con el mismo código de error.
        if ((await new ResolutorDeIdentificador(centralIdentity).ResolverAsync(nuevo, ct)).Cuenta is not null)
            return Result.Failure<CambioDeCorreoSolicitado>("Identity.Email.Taken", "Ese correo ya lo usa otra cuenta.");

        var now = clock.UtcNow;
        var vivas = await db.EmailChangeRequests
            .Where(r => r.CentralUserId == id && r.Status == EmailChangeStatus.Pending)
            .ToListAsync(ct);
        foreach (var viva in vivas) viva.Cancelar();

        var codigo = CodigoDeCambioDeCorreo.Nuevo();
        db.EmailChangeRequests.Add(EmailChangeRequest.Solicitar(
            id, nuevo, nuevo.ToUpperInvariant(), CodigoDeCambioDeCorreo.Huella(id, codigo), now, request.IpAddress));
        await db.SaveChangesAsync(ct);

        await correos.EnviarAsync(nuevo, "EmailChangeCodeEmail", "Confirma tu nuevo correo — IngenIA365ERP",
            new Dictionary<string, string?>
            {
                ["Nombre"] = persona.NombreParaMostrar,
                ["Codigo"] = codigo,
                ["Minutos"] = EmailChangeRequest.MinutosDeVigencia.ToString(),
            }, ct);
        await auditoria.GlobalAsync(
            AuditEventTypes.IdentityEmailChangeRequested, id, persona.Email, nameof(CentralUser), id,
            new { persona.Email }, new { Email = nuevo }, now, ct);

        return Result.Success(new CambioDeCorreoSolicitado(EmailChangeRequest.MinutosDeVigencia * 60));
    }
}

public sealed class ConfirmarCambioDeCorreoCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext db,
    PropagadorDeIdentidad propagador,
    CorreosDeIdentidad correos,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<ConfirmarCambioDeCorreoCommand, Result<CorreoConfirmado>>
{
    public async Task<Result<CorreoConfirmado>> Handle(ConfirmarCambioDeCorreoCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is not { } id
            || currentUser.Purpose is not (CentralJwtPurposes.Full or CentralJwtPurposes.StepUp))
            return Result.Failure<CorreoConfirmado>("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var now = clock.UtcNow;
        var pedido = await db.EmailChangeRequests
            .Where(r => r.CentralUserId == id && r.Status == EmailChangeStatus.Pending && !r.ByMaster)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (pedido is null || !pedido.Vigente(now))
            return Result.Failure<CorreoConfirmado>(
                "Identity.Email.CodeExpired", "El código venció o se agotaron los intentos. Pida uno nuevo.");

        if (!CryptographicOperations.FixedTimeEquals(pedido.CodeHash, CodigoDeCambioDeCorreo.Huella(id, request.Code ?? string.Empty)))
        {
            pedido.IntentoFallido();
            await db.SaveChangesAsync(ct);
            return Result.Failure<CorreoConfirmado>(
                "Identity.Email.CodeInvalid",
                $"El código no es correcto. Le quedan {Math.Max(0, EmailChangeRequest.IntentosMaximos - pedido.Attempts)} intentos.");
        }

        var persona = await centralIdentity.FindByIdAsync(id, ct);
        if (persona is null)
            return Result.Failure<CorreoConfirmado>("Identity.Unauthenticated", "Inicie sesión de nuevo.");
        var anterior = persona.Email;

        var cambio = await centralIdentity.ChangeEmailAsync(id, pedido.NewEmail, now, ct);
        if (!cambio.Succeeded)
            return cambio.ErrorCodes.Contains("Identity.DuplicateEmail")
                ? Result.Failure<CorreoConfirmado>("Identity.Email.Taken", "Ese correo ya lo usa otra cuenta.")
                : Result.Failure<CorreoConfirmado>(cambio.ErrorCodes.FirstOrDefault() ?? "Identity.Email.Invalid", "No se pudo cambiar el correo.");

        pedido.Confirmar();
        await db.SaveChangesAsync(ct);
        await propagador.CorreoAsync(id, pedido.NewEmail, ct);

        await auditoria.GlobalAsync(
            AuditEventTypes.IdentityEmailChanged, id, pedido.NewEmail, nameof(CentralUser), id,
            new { Email = anterior }, new { Email = pedido.NewEmail }, now, ct);
        await correos.EnviarAsync(anterior, "EmailChangedNoticeEmail", "El correo de tu cuenta cambió",
            new Dictionary<string, string?>
            {
                ["Nombre"] = persona.NombreParaMostrar,
                ["Quien"] = "Tú mismo",
                ["CorreoNuevo"] = pedido.NewEmail,
            }, ct);

        return Result.Success(new CorreoConfirmado(pedido.NewEmail));
    }
}
