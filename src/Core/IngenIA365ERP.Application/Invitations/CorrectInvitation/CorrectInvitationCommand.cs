using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Application.Invitations.GestionInvitaciones;
using IngenIA365ERP.Application.Invitations.IssueTenantInvitation;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Invitations.CorrectInvitation;

/// <summary>
/// Corrige una invitación pendiente o en revisión (feature 017, FR-032, D-06): la anterior queda
/// <c>Superseded</c> y sale otra con los datos corregidos, con los intentos en cero. Es la salida de
/// una invitación en revisión, además de cancelarla.
/// <para>
/// Si se corrige sólo el correo de una identidad que nació con esta invitación y nunca se activó, se
/// le cambia el correo a esa identidad (research R7). Si la identidad ya tiene otra invitación viva
/// en otra cooperativa, su correo no se toca: lo corrige quien la creó.
/// </para>
/// </summary>
public sealed record CorrectInvitationCommand(
    Guid InvitationPublicId,
    string Email,
    string DocumentType,
    string DocumentNumber,
    string DocumentNumberConfirmation,
    string FirstName,
    string? OtherNames,
    string LastName,
    string? SecondLastName,
    string? MobilePhone,
    Guid? RolePublicId,
    Guid? PositionPublicId
) : IRequest<Result<IssueTenantInvitationResult>>
{
    public DatosDelInvitado Datos() => new(
        Email, DocumentType, DocumentNumber, DocumentNumberConfirmation,
        FirstName, OtherNames, LastName, SecondLastName, MobilePhone, RolePublicId, PositionPublicId);
}

public sealed class CorrectInvitationCommandValidator : AbstractValidator<CorrectInvitationCommand>
{
    public CorrectInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationPublicId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(3);
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DocumentNumberConfirmation).NotEmpty().WithMessage("Digite otra vez el número de documento.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.OtherNames).MaximumLength(60);
        RuleFor(x => x.SecondLastName).MaximumLength(60);
        RuleFor(x => x.MobilePhone).MaximumLength(30);
    }
}

public sealed class CorrectInvitationCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    ICentralIdentityProvider identidad,
    EmisionDeInvitacion emision,
    IInvitationEmailDispatcher correo,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock,
    ILogger<CorrectInvitationCommandHandler> logger)
    : IRequestHandler<CorrectInvitationCommand, Result<IssueTenantInvitationResult>>
{
    public async Task<Result<IssueTenantInvitationResult>> Handle(CorrectInvitationCommand request, CancellationToken ct)
    {
        var original = await db.Invitations.FirstOrDefaultAsync(i => i.PublicId == request.InvitationPublicId, ct);
        if (original is null)
            return Result.Failure<IssueTenantInvitationResult>("Invitation.NotFound", "No existe la invitación indicada.");

        var autorizacion = await GestionInvitacionesGuardias.VerificarAdministradorAsync(currentUser, db, original.TenantId, ct);
        if (autorizacion is not null)
            return Result.Failure<IssueTenantInvitationResult>(autorizacion.Code, autorizacion.Message);

        if (original.Status is not (InvitationStatus.Pending or InvitationStatus.UnderReview))
            return Result.Failure<IssueTenantInvitationResult>("Invitation.NotPending",
                "Sólo se corrige una invitación pendiente o en revisión.");

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == original.TenantId && t.IsActive, ct);
        if (tenant is null)
            return Result.Failure<IssueTenantInvitationResult>("Tenant.NotFound", "La cooperativa de esa invitación no existe o está inactiva.");

        // Mismo documento y correo distinto en una identidad que nunca se activó: se corrige su correo.
        var nuevoUsuario = UsuarioDeDocumento.Validar(request.DocumentType, request.DocumentNumber, soloDeAcceso: true, out _)?.Usuario;
        if (original.CentralUserId is { } ancla && nuevoUsuario is not null)
        {
            var identidadAnclada = await identidad.FindByIdAsync(ancla, ct);
            if (identidadAnclada is { Status: CentralUserStatus.Pending }
                && identidadAnclada.DocumentUsername == nuevoUsuario
                && !string.Equals(identidadAnclada.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var otrasVivas = await db.Invitations.AnyAsync(i =>
                    i.CentralUserId == ancla && i.TenantId != original.TenantId
                    && (i.Status == InvitationStatus.Pending || i.Status == InvitationStatus.UnderReview), ct);
                if (!otrasVivas)
                {
                    var cambio = await identidad.UpdatePendingEmailAsync(ancla, request.Email.Trim(), ct);
                    if (!cambio.Succeeded && cambio.ErrorCodes.Contains("Identity.DuplicateEmail"))
                        return Result.Failure<IssueTenantInvitationResult>(IdentityErrors.CorreoYaRegistrado());
                }
            }
        }

        // La anterior deja de valer antes de emitir la nueva (si el documento cambió de persona, la
        // emisión no la encontraría por correo ni por identidad).
        original.MarkSuperseded();

        var preparada = await emision.PrepararAsync(tenant, currentUser.CentralUserId!.Value, original.InviteAsTenantAdmin, request.Datos(), ct);
        if (preparada.IsFailure)
            return Result.Failure<IssueTenantInvitationResult>(preparada.Error);

        var nueva = preparada.Value.Invitacion;
        await db.SaveChangesAsync(ct);

        var ahora = clock.UtcNow;
        await auditoria.EnCooperativaAsync(
            tenant.PublicId, AuditEventTypes.InvitationCorrected, currentUser.CentralUserId!.Value, currentUser.Email,
            nameof(Invitation), original.PublicId,
            new { original.DocumentType, Documento = ListarInvitacionesQueryHandler.Enmascarar(original.DocumentNumber), original.Email },
            new { nueva.PublicId, nueva.DocumentType, Documento = ListarInvitacionesQueryHandler.Enmascarar(nueva.DocumentNumber) },
            ahora, ct);

        // Fail-soft, como el reenvío: la invitación corregida queda aunque el correo no salga.
        try
        {
            await correo.DispatchAsync(new InvitationEmailRequest(
                nueva, tenant, currentUser.Email ?? "Administrador", preparada.Value.TokenPlano,
                preparada.Value.NombreDelDestinatario), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Invitación {Anterior} corregida como {Nueva}, pero el correo no salió.", original.PublicId, nueva.PublicId);
        }

        return Result.Success(new IssueTenantInvitationResult(nueva.PublicId, nueva.ExpiresAt, MensajesDeInvitacion.Enviada));
    }
}
