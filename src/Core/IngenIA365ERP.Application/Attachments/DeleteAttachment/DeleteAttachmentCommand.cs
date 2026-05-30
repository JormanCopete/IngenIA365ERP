using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
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

    public DeleteAttachmentCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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

        if (attachment is null)
        {
            return Result.Failure("Generic.NotFound", "Adjunto no encontrado.");
        }

        // El SoftDeleteInterceptor traduce Remove() → soft-delete automáticamente.
        _db.Attachments.Remove(attachment);
        attachment.UpdatedBy = _currentUser.UserName ?? "SYSTEM";
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
