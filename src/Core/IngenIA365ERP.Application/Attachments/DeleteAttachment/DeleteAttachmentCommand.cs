using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Attachments.DeleteAttachment;

/// <summary>
/// T108 — Soft-delete del adjunto (FR-029). El blob físico se conserva en
/// el store hasta el GC programado — esto permite restauración rápida si
/// el borrado fue accidental. Cuando aterrice el job de retention, los
/// blobs con <c>IsDeleted = 1</c> y <c>DeletedAt &lt; now - 30 días</c> se
/// purgan del filesystem.
///
/// <para>
/// Un adjunto que generó un módulo y éste declara inmutable (<see cref="AdjuntosDeModulo"/>: el PDF
/// de la definitiva) no se borra por aquí: responde <c>Attachments.OwnedByModule</c>. Hasta el
/// 2026-09-21 el Operador, que tiene <c>Attachments.*</c>, podía borrarlo y dejar la terminación
/// apuntando a un adjunto eliminado.
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
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionChecker _permissions;

    public DeleteAttachmentCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IPermissionChecker permissions)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
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

        if (AdjuntosDeModulo.De(attachment.OwnerEntityType) is { Borrable: false } regla)
        {
            return Result.Failure(AdjuntosDeModulo.NoBorrable(regla));
        }

        // El SoftDeleteInterceptor traduce Remove() → soft-delete automáticamente.
        _db.Attachments.Remove(attachment);
        attachment.UpdatedBy = _currentUser.UserName ?? "SYSTEM";
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
