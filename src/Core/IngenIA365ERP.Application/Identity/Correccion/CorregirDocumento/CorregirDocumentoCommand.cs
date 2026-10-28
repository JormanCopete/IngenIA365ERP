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

namespace IngenIA365ERP.Application.Identity.Correccion.CorregirDocumento;

/// <summary>
/// Feature 017 (US6, FR-031..FR-034): corregir (se digitó mal) o actualizar (cambió, por ejemplo de TI a CC)
/// el documento de una identidad. Lo hacen dos actores, y quién puede lo decide el servidor:
/// <list type="bullet">
///   <item>el administrador de la cooperativa, por <c>SEC_Users.PublicId</c>, sólo si la persona no tiene
///         membresía en ninguna otra cooperativa; si la tiene, <c>Identity.Document.PlatformOnly</c> (el cambio
///         la afectaría en cooperativas que este administrador no ve);</item>
///   <item>el maestro de plataforma, por la identidad central, sin ese límite
///         (<see cref="CorregirDocumentoPorMaestroCommand"/>).</item>
/// </list>
/// Motivo obligatorio, historial con lo anterior, usuario recalculado y llevado a cada cooperativa, evento
/// <c>Identity.Document.Corrected</c>/<c>.Updated</c> y correo a la persona con su usuario nuevo. La clave, el
/// correo y el segundo factor no se tocan.
/// </summary>
public sealed record CorregirDocumentoCommand(
    Guid UserPublicId,
    string? DocumentType,
    string? DocumentNumber,
    string? DocumentNumberConfirmation,
    string? Reason,
    string? Notes,
    Guid? SupportAttachmentPublicId) : IRequest<Result<DocumentoCorregidoResult>>;

public sealed record DocumentoCorregidoResult(Guid CentralUserId, string DocumentUsername, string? PreviousUsername);

public sealed class CorregirDocumentoCommandValidator : AbstractValidator<CorregirDocumentoCommand>
{
    public CorregirDocumentoCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty().WithMessage("El tipo de documento es obligatorio.").MaximumLength(3);
        RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("El número de documento es obligatorio.").MaximumLength(30);
        RuleFor(x => x.DocumentNumberConfirmation).NotEmpty().WithMessage("Digite otra vez el número de documento.").MaximumLength(30);
        RuleFor(x => x.Reason)
            .Must(r => r is nameof(DocumentChangeReason.Correction) or nameof(DocumentChangeReason.Update))
            .WithMessage("El motivo es «Correction» (se digitó mal) o «Update» (el documento cambió).");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class CorregirDocumentoCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext admin,
    IApplicationDbContext cooperativa,
    CorreccionDeDocumento correccion)
    : IRequestHandler<CorregirDocumentoCommand, Result<DocumentoCorregidoResult>>
{
    public async Task<Result<DocumentoCorregidoResult>> Handle(CorregirDocumentoCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null || currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure<DocumentoCorregidoResult>("Identity.Unauthenticated", "Inicie sesión de nuevo.");
        if (currentUser.ActiveTenantPublicId is not { } activa)
            return Result.Failure<DocumentoCorregidoResult>("Tenant.NotResolved", "No hay una cooperativa activa.");

        var persona = await IdentidadDelUsuario.ResolverAsync(cooperativa, centralIdentity, request.UserPublicId, ct);
        if (persona is null)
            return Result.Failure<DocumentoCorregidoResult>("User.NotFound", "Usuario no encontrado.");

        var otras = await admin.TenantMemberships.AsNoTracking()
            .AnyAsync(m => m.CentralUserId == persona && m.TenantId != activa && m.Status != MembershipStatus.Revoked, ct);
        if (otras)
            return Result.Failure<DocumentoCorregidoResult>(
                "Identity.Document.PlatformOnly",
                "Esta persona también está en otra cooperativa: su documento sólo lo puede corregir soporte de la plataforma.");

        return await correccion.CorregirAsync(persona.Value, DocumentChangeActor.TenantAdmin, activa, request, ct);
    }
}

/// <summary>Feature 017 (US6): la misma corrección hecha por el maestro, por la identidad central y sin límite de cooperativas.</summary>
public sealed record CorregirDocumentoPorMaestroCommand(Guid CentralUserId, CorregirDocumentoCommand Datos)
    : IRequest<Result<DocumentoCorregidoResult>>;

public sealed class CorregirDocumentoPorMaestroCommandHandler(
    ICurrentCentralUserContext currentUser,
    CorreccionDeDocumento correccion)
    : IRequestHandler<CorregirDocumentoPorMaestroCommand, Result<DocumentoCorregidoResult>>
{
    public async Task<Result<DocumentoCorregidoResult>> Handle(CorregirDocumentoPorMaestroCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.Purpose != CentralJwtPurposes.Full || !currentUser.IsGlobalMasterAdmin)
            return Result.Failure<DocumentoCorregidoResult>("Saas.MasterOnly", "Sólo el maestro de plataforma.");
        return await correccion.CorregirAsync(request.CentralUserId, DocumentChangeActor.PlatformMaster, null, request.Datos, ct);
    }
}

/// <summary>
/// La identidad central de un usuario de la cooperativa activa: por su vínculo <c>CentralUserId</c> y, en filas
/// de antes del vínculo, por el correo verificado (con el resolvedor de «Correo o usuario»).
/// </summary>
public static class IdentidadDelUsuario
{
    public static async Task<Guid?> ResolverAsync(
        IApplicationDbContext cooperativa, ICentralIdentityProvider centralIdentity, Guid userPublicId, CancellationToken ct)
    {
        var fila = await cooperativa.Users.AsNoTracking()
            .Where(u => u.PublicId == userPublicId)
            .Select(u => new { u.CentralUserId, u.Email })
            .FirstOrDefaultAsync(ct);
        if (fila is null) return null;
        if (fila.CentralUserId is { } vinculo) return vinculo;
        return (await new Auth.ResolutorDeIdentificador(centralIdentity).ResolverAsync(fila.Email, ct)).Cuenta?.Id;
    }
}

/// <summary>
/// Lo común de las dos correcciones: validar, guardar con historial, llevar el usuario a cada cooperativa,
/// auditar y avisar. Quién puede corregir a quién lo decidió ya el manejador que la llama.
/// </summary>
public sealed class CorreccionDeDocumento(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext admin,
    PropagadorDeIdentidad propagador,
    CorreosDeIdentidad correos,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock,
    ILogger<CorreccionDeDocumento> logger)
{
    public async Task<Result<DocumentoCorregidoResult>> CorregirAsync(
        Guid persona, DocumentChangeActor tipoDeActor, Guid? cooperativa, CorregirDocumentoCommand request, CancellationToken ct)
    {
        var actor = currentUser.CentralUserId!.Value;
        var cuenta = await centralIdentity.FindByIdAsync(persona, ct);
        if (cuenta is null || cuenta.Status == CentralUserStatus.Disabled)
            return Result.Failure<DocumentoCorregidoResult>("User.NotFound", "Usuario no encontrado.");

        if (UsuarioDeDocumento.NormalizarNumero(request.DocumentNumber)
            != UsuarioDeDocumento.NormalizarNumero(request.DocumentNumberConfirmation))
            return Result.Failure<DocumentoCorregidoResult>(IdentityErrors.DocumentoNoCoincide());
        var documento = UsuarioDeDocumento.Validar(request.DocumentType, request.DocumentNumber, soloDeAcceso: true, out var motivo);
        if (documento is null)
            return Result.Failure<DocumentoCorregidoResult>(IdentityErrors.DocumentoInvalido(motivo ?? "Documento no válido."));
        if (documento.Usuario == cuenta.DocumentUsername)
            return Result.Failure<DocumentoCorregidoResult>(
                "Identity.Document.Unchanged", "Ese ya es el documento registrado de esta persona.");
        if (!Enum.TryParse<DocumentChangeReason>(request.Reason, out var razon)
            || razon is not (DocumentChangeReason.Correction or DocumentChangeReason.Update))
            return Result.Failure<DocumentoCorregidoResult>(
                "Validation.Invalid", "El motivo es «Correction» (se digitó mal) o «Update» (el documento cambió).");

        var cooperativas = await admin.TenantMemberships.AsNoTracking()
            .Where(m => m.CentralUserId == persona && m.Status != MembershipStatus.Revoked)
            .Select(m => m.TenantId).ToListAsync(ct);
        var politicas = await admin.TenantIdentityPolicies.AsNoTracking()
            .Where(p => cooperativas.Contains(p.TenantId)).Select(p => p.AcceptedDocumentTypes).ToListAsync(ct);
        if (politicas.Any(tipos => TenantIdentityPolicy.TiposAceptadosDe(tipos).All(t => t.Codigo != documento.Tipo.Codigo)))
            return Result.Failure<DocumentoCorregidoResult>(IdentityErrors.TipoNoAceptado(documento.Tipo.Codigo));

        var now = clock.UtcNow;
        var anterior = (cuenta.DocumentType, cuenta.DocumentNumber, cuenta.DocumentUsername);

        var registro = await centralIdentity.SetDocumentAsync(persona, documento, ct);
        if (!registro.Succeeded)
        {
            if (registro.ErrorCodes.Contains("Identity.DuplicateDocument"))
            {
                var duena = await centralIdentity.FindByDocumentUsernameAsync(documento.Usuario, ct);
                admin.IdentityCases.Add(IdentityCase.Abrir(
                    IdentityCaseKind.DocumentTaken, persona, duena?.Id,
                    $"{documento.Tipo.Codigo} {documento.Numero}: corrección pedida por {tipoDeActor}; ya es de otra identidad.",
                    openedBy: actor));
                await admin.SaveChangesAsync(ct);
                return Result.Failure<DocumentoCorregidoResult>(
                    "Identity.Document.Unavailable",
                    "Ese documento ya pertenece a otra cuenta. Se abrió un caso para que soporte revise si son la misma persona.");
            }
            return Result.Failure<DocumentoCorregidoResult>(
                registro.ErrorCodes.FirstOrDefault() ?? "Identity.Document.Invalid", "No se pudo corregir el documento.");
        }

        admin.DocumentHistory.Add(DocumentHistoryEntry.Registrar(
            persona, anterior.DocumentType, anterior.DocumentNumber, anterior.DocumentUsername,
            documento.Tipo.Codigo, documento.Numero, documento.Usuario,
            razon, tipoDeActor, actor, now,
            notes: request.Notes,
            supportAttachmentPublicId: request.SupportAttachmentPublicId,
            supportTenantId: request.SupportAttachmentPublicId is null ? null : cooperativa));
        await admin.SaveChangesAsync(ct);

        var pendientes = await propagador.UsuarioAsync(persona, documento.Usuario, ct);
        if (pendientes.Count > 0)
            logger.LogWarning("[Identidad.Correccion] El usuario de {Persona} quedó pendiente en {N} cooperativas.", persona, pendientes.Count);

        var evento = razon == DocumentChangeReason.Correction
            ? AuditEventTypes.IdentityDocumentCorrected
            : AuditEventTypes.IdentityDocumentUpdated;
        var datosAnteriores = new { Tipo = anterior.DocumentType, Usuario = anterior.DocumentUsername };
        var datosNuevos = new { Tipo = documento.Tipo.Codigo, Usuario = documento.Usuario, request.Notes };
        if (cooperativa is { } c)
            await auditoria.EnCooperativaAsync(c, evento, actor, currentUser.Email, nameof(CentralUser), persona, datosAnteriores, datosNuevos, now, ct);
        await auditoria.GlobalAsync(evento, actor, currentUser.Email, nameof(CentralUser), persona, datosAnteriores, datosNuevos, now, ct);

        await correos.EnviarAsync(cuenta.Email, "UsernameChangedEmail", "Tu usuario de IngenIA365ERP cambió",
            new Dictionary<string, string?>
            {
                ["Nombre"] = cuenta.NombreParaMostrar,
                ["Usuario"] = documento.Usuario,
                ["Quien"] = tipoDeActor == DocumentChangeActor.PlatformMaster ? "Soporte de la plataforma" : "El administrador de tu cooperativa",
            }, ct);

        return Result.Success(new DocumentoCorregidoResult(persona, documento.Usuario, anterior.DocumentUsername));
    }
}
