using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Documento;

/// <summary>
/// Feature 017 (US5): la persona que ya tenía cuenta completa su documento. Lo usa con su sesión (el paso
/// posponible) o con el desafío <c>document-required</c> (cuando una cooperativa ya lo exige). Sólo la
/// propia persona registra el suyo, y sólo una vez: cambiarlo después es una corrección del administrador
/// o del maestro. Si el documento ya es de otra identidad no se dice de quién: se responde
/// <c>Identity.Document.Unavailable</c> y se abre un caso <c>DocumentTaken</c> para soporte.
/// </summary>
public sealed record RegistrarDocumentoCommand(
    string? DocumentType,
    string? DocumentNumber,
    string? DocumentNumberConfirmation) : IRequest<Result<DocumentoRegistradoResult>>;

public sealed record DocumentoRegistradoResult(string DocumentUsername);

public sealed class RegistrarDocumentoCommandValidator : AbstractValidator<RegistrarDocumentoCommand>
{
    public RegistrarDocumentoCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty().WithMessage("El tipo de documento es obligatorio.").MaximumLength(3);
        RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("El número de documento es obligatorio.").MaximumLength(30);
        RuleFor(x => x.DocumentNumberConfirmation).NotEmpty().WithMessage("Digite otra vez el número de documento.").MaximumLength(30);
    }
}

public sealed class RegistrarDocumentoCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    IAdminDbContext db,
    PropagadorDeIdentidad propagador,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock,
    ILogger<RegistrarDocumentoCommandHandler> logger)
    : IRequestHandler<RegistrarDocumentoCommand, Result<DocumentoRegistradoResult>>
{
    public async Task<Result<DocumentoRegistradoResult>> Handle(RegistrarDocumentoCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is not { } id)
            return Result.Failure<DocumentoRegistradoResult>("Identity.Unauthenticated", "Inicie sesión de nuevo.");
        if (currentUser.Purpose is not (CentralJwtPurposes.Full or CentralJwtPurposes.DocumentRequired))
            return Result.Failure<DocumentoRegistradoResult>(
                "Identity.WrongTokenPurpose", "Este paso exige la sesión o el desafío de documento.");

        var persona = await centralIdentity.FindByIdAsync(id, ct);
        if (persona is null)
            return Result.Failure<DocumentoRegistradoResult>("Identity.Unauthenticated", "Inicie sesión de nuevo.");
        if (!string.IsNullOrEmpty(persona.DocumentUsername))
            return Result.Failure<DocumentoRegistradoResult>(
                "Identity.Document.AlreadyRegistered",
                "Su documento ya está registrado. Para corregirlo, comuníquese con el administrador de su cooperativa.");

        if (UsuarioDeDocumento.NormalizarNumero(request.DocumentNumber)
            != UsuarioDeDocumento.NormalizarNumero(request.DocumentNumberConfirmation))
            return Result.Failure<DocumentoRegistradoResult>(IdentityErrors.DocumentoNoCoincide());

        var documento = UsuarioDeDocumento.Validar(request.DocumentType, request.DocumentNumber, soloDeAcceso: true, out var motivo);
        if (documento is null)
            return Result.Failure<DocumentoRegistradoResult>(IdentityErrors.DocumentoInvalido(motivo ?? "Documento no válido."));

        // El tipo tiene que servir en TODAS sus cooperativas activas: si una no lo acepta, esa persona
        // no podría entrar allí con su usuario.
        var activas = await memberships.GetActiveMembershipsAsync(id, ct);
        var ids = activas.Select(m => m.TenantId).ToList();
        var politicas = await db.TenantIdentityPolicies.AsNoTracking()
            .Where(p => ids.Contains(p.TenantId))
            .Select(p => p.AcceptedDocumentTypes)
            .ToListAsync(ct);
        if (politicas.Any(tipos => TenantIdentityPolicy.TiposAceptadosDe(tipos).All(t => t.Codigo != documento.Tipo.Codigo)))
            return Result.Failure<DocumentoRegistradoResult>(IdentityErrors.TipoNoAceptado(documento.Tipo.Codigo));

        var now = clock.UtcNow;
        var registro = await centralIdentity.SetDocumentAsync(id, documento, ct);
        if (!registro.Succeeded)
        {
            if (registro.ErrorCodes.Contains("Identity.DuplicateDocument"))
            {
                var duena = await centralIdentity.FindByDocumentUsernameAsync(documento.Usuario, ct);
                db.IdentityCases.Add(IdentityCase.Abrir(
                    IdentityCaseKind.DocumentTaken, id, duena?.Id,
                    $"{documento.Tipo.Codigo} {documento.Numero}: la persona lo registró y ya es de otra identidad.",
                    openedBy: id));
                await db.SaveChangesAsync(ct);
                logger.LogWarning("[Identidad.DocumentoTomado] {CentralUserId} intentó registrar un documento ajeno.", id);
                return Result.Failure<DocumentoRegistradoResult>(IdentityErrors.DocumentoNoDisponible());
            }
            return Result.Failure<DocumentoRegistradoResult>(
                registro.ErrorCodes.FirstOrDefault() ?? "Identity.Document.Invalid", "No se pudo registrar el documento.");
        }

        db.DocumentHistory.Add(DocumentHistoryEntry.Registrar(
            id, null, null, null, documento.Tipo.Codigo, documento.Numero, documento.Usuario,
            DocumentChangeReason.Registration, DocumentChangeActor.Person, id, now,
            notes: "Registrado por la persona."));
        await db.SaveChangesAsync(ct);

        await propagador.UsuarioAsync(id, documento.Usuario, ct);
        await auditoria.GlobalAsync(
            AuditEventTypes.IdentityDocumentRegistered, id, persona.Email, nameof(CentralUser), id,
            null, new { documento.Tipo.Codigo, Usuario = documento.Usuario }, now, ct);

        return Result.Success(new DocumentoRegistradoResult(documento.Usuario));
    }
}
