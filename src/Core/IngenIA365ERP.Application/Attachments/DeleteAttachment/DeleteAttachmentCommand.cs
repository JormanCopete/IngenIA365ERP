using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Attachments.DeleteAttachment;

/// <summary>
/// T108 — Borrar un adjunto (FR-029). Es siempre el acto explícito de una persona con
/// <c>Attachments.Delete</c>, auditado (<c>AuditBehavior</c>): nada en el ERP borra adjuntos por su
/// cuenta, y lo vigila <c>NadieBorraAdjuntosPorSuCuenta</c>.
///
/// <para>
/// Feature 011 (research R8): primero se retira el objeto del almacén y después se da de baja la fila.
/// En un bucket versionado, retirar deja una marca de borrado y la versión queda 90 días en la papelera,
/// de donde soporte la recupera (docs/operaciones/adjuntos-en-s3.md). Si falla la baja de la fila, el
/// objeto ya está en la papelera y la fila sigue viva: la descarga dice «no está» y reintentar completa,
/// porque retirar es idempotente. En el orden inverso, un almacén caído dejaba la fila borrada y el
/// objeto vigente, sin nada que lo nombrara ni regla que lo purgara. Hasta el 2026-09-23 sólo se daba de
/// baja la fila, y este comentario prometía un «GC programado» que purgaría el archivo: nunca existió.
/// </para>
///
/// <para>
/// Las reglas de conservación las decide <see cref="AdjuntosDeModulo.PuedeBorrarAsync"/>: lo que
/// genera un módulo no se borra (<c>Attachments.OwnedByModule</c>; hasta el 2026-09-21 el Operador
/// borraba el PDF de la definitiva), y el soporte de un comprobante contabilizado tampoco
/// (<c>Attachments.OwnerLocked</c>; hasta el 2026-09-23 sólo la pantalla escondía el botón).
/// </para>
/// </summary>
public sealed record DeleteAttachmentCommand(Guid AttachmentPublicId) : IRequest<Result>;

public sealed class DeleteAttachmentCommandValidator : AbstractValidator<DeleteAttachmentCommand>
{
    public DeleteAttachmentCommandValidator()
    {
        RuleFor(x => x.AttachmentPublicId).NotEmpty();
    }
}

public sealed class DeleteAttachmentCommandHandler
    : IRequestHandler<DeleteAttachmentCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IBlobStore _store;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionChecker _permissions;
    private readonly ILogger<DeleteAttachmentCommandHandler> _log;

    public DeleteAttachmentCommandHandler(
        IApplicationDbContext db,
        IBlobStore store,
        ICurrentUserService currentUser,
        IPermissionChecker permissions,
        ILogger<DeleteAttachmentCommandHandler> log)
    {
        _db = db;
        _store = store;
        _currentUser = currentUser;
        _permissions = permissions;
        _log = log;
    }

    public async Task<Result> Handle(DeleteAttachmentCommand request, CancellationToken ct)
    {
        var tenantIdStr = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantIdStr) || !int.TryParse(tenantIdStr, out var tenantInternalId))
        {
            return Result.Failure("Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var attachment = await _db.Attachments
            .Where(a => a.TenantId == tenantInternalId && a.PublicId == request.AttachmentPublicId)
            .FirstOrDefaultAsync(ct);

        if (attachment is null || !await AdjuntosDeModulo.PuedeLeerAsync(_permissions, attachment.OwnerEntityType, ct))
        {
            return Result.Failure("Generic.NotFound", "Adjunto no encontrado.");
        }

        if (await AdjuntosDeModulo.PuedeBorrarAsync(_db, attachment.OwnerEntityType, attachment.OwnerEntityPublicId, ct, _permissions) is { } prohibido)
        {
            return Result.Failure(prohibido);
        }

        // R8: primero el objeto (a la papelera), después la fila. Una fila sin objeto —una subida que
        // nunca llegó— no tiene nada que retirar.
        if (!string.IsNullOrWhiteSpace(attachment.StoragePath))
        {
            await _store.DeleteAsync(new BlobReference(attachment.StoragePath), ct);
        }

        // El SoftDeleteInterceptor traduce Remove() → soft-delete automáticamente.
        _db.Attachments.Remove(attachment);
        attachment.UpdatedBy = _currentUser.UserName ?? "SYSTEM";
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Adjunto {Adjunto} de la cooperativa {Cooperativa} borrado por {Usuario}; el objeto queda en la papelera del almacén.",
            attachment.PublicId, tenantInternalId, attachment.UpdatedBy);
        return Result.Success();
    }
}
