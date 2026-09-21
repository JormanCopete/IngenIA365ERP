using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Attachments.ListAttachments;

/// <summary>
/// Lista los adjuntos vinculados a una entidad propietaria (US5). Usado
/// por <c>AttachmentList.razor</c> (T111) para renderizar la galería de
/// archivos del User/Loan/Transaction actual. Sin paginación — en la
/// práctica una entidad rara vez excede ~50 adjuntos; si se necesita se
/// añade un PageRequest. Los dueños que gobierna un módulo (<see cref="AdjuntosDeModulo"/>)
/// exigen además su permiso: sin él la lista sale vacía, como si no hubiera nada.
/// </summary>
public sealed record ListAttachmentsByOwnerQuery(
    string OwnerEntityType,
    Guid OwnerEntityPublicId) : IRequest<Result<IReadOnlyList<AttachmentDto>>>;

public sealed class ListAttachmentsByOwnerQueryValidator
    : AbstractValidator<ListAttachmentsByOwnerQuery>
{
    public ListAttachmentsByOwnerQueryValidator()
    {
        RuleFor(x => x.OwnerEntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerEntityPublicId).NotEmpty();
    }
}

public sealed class ListAttachmentsByOwnerQueryHandler
    : IRequestHandler<ListAttachmentsByOwnerQuery, Result<IReadOnlyList<AttachmentDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionChecker _permissions;

    public ListAttachmentsByOwnerQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IPermissionChecker permissions)
    {
        _db = db;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Result<IReadOnlyList<AttachmentDto>>> Handle(
        ListAttachmentsByOwnerQuery request, CancellationToken ct)
    {
        var tenantIdStr = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantIdStr) || !int.TryParse(tenantIdStr, out var tenantInternalId))
        {
            return Result.Failure<IReadOnlyList<AttachmentDto>>(
                "Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        if (!await AdjuntosDeModulo.PuedeLeerAsync(_permissions, request.OwnerEntityType, ct))
            return Result.Success<IReadOnlyList<AttachmentDto>>([]);

        var items = await _db.Attachments
            .Where(a => a.TenantId == tenantInternalId
                     && a.OwnerEntityType == request.OwnerEntityType
                     && a.OwnerEntityPublicId == request.OwnerEntityPublicId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto(
                a.PublicId,
                a.OwnerEntityType,
                a.OwnerEntityPublicId,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.Sha256Hex,
                a.CreatedAt,
                a.CreatedBy))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AttachmentDto>>(items);
    }
}
