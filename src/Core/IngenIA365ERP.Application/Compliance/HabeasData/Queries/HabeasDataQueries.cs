using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.HabeasData.Queries;

/// <summary>Lista todas las versiones publicadas por el tenant (más recientes primero).</summary>
public sealed record ListPoliciesQuery : IRequest<Result<IReadOnlyList<HabeasDataPolicyDto>>>;

/// <summary>Detalle de una versión específica (incluye contenido).</summary>
public sealed record GetPolicyByPublicIdQuery(Guid PublicId)
    : IRequest<Result<HabeasDataPolicyDetailDto>>;

/// <summary>Historial cronológico de consentimientos/revocaciones de un titular.</summary>
public sealed record ListHabeasDataHistoryQuery(int PersonId)
    : IRequest<Result<IReadOnlyList<HabeasDataHistoryItemDto>>>;

// Validators triviales — requeridos por Principio VIII (todo IRequest debe
// tener su AbstractValidator hermano, incluso si no hay reglas de validación).
public sealed class ListPoliciesQueryValidator : AbstractValidator<ListPoliciesQuery>;

public sealed class GetPolicyByPublicIdQueryValidator
    : AbstractValidator<GetPolicyByPublicIdQuery>
{
    public GetPolicyByPublicIdQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class ListHabeasDataHistoryQueryValidator
    : AbstractValidator<ListHabeasDataHistoryQuery>
{
    public ListHabeasDataHistoryQueryValidator() => RuleFor(x => x.PersonId).GreaterThan(0);
}

public sealed class ListPoliciesQueryHandler
    : IRequestHandler<ListPoliciesQuery, Result<IReadOnlyList<HabeasDataPolicyDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ListPoliciesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db; _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<HabeasDataPolicyDto>>> Handle(
        ListPoliciesQuery request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.TenantId, out var tenantId))
        {
            return Result.Failure<IReadOnlyList<HabeasDataPolicyDto>>(
                "Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");
        }

        var items = await _db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.VersionNumber)
            .Select(p => new HabeasDataPolicyDto(
                p.PublicId,
                p.VersionNumber,
                p.Title,
                p.Sha256Hex,
                p.EffectiveFrom,
                p.EffectiveTo,
                p.PublishedBy,
                p.EffectiveTo == null))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<HabeasDataPolicyDto>>(items);
    }
}

public sealed class GetPolicyByPublicIdQueryHandler
    : IRequestHandler<GetPolicyByPublicIdQuery, Result<HabeasDataPolicyDetailDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPolicyByPublicIdQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db; _currentUser = currentUser;
    }

    public async Task<Result<HabeasDataPolicyDetailDto>> Handle(
        GetPolicyByPublicIdQuery request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.TenantId, out var tenantId))
        {
            return Result.Failure<HabeasDataPolicyDetailDto>(
                "Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");
        }

        var dto = await _db.HabeasDataPolicyVersions
            .Where(p => p.TenantId == tenantId && p.PublicId == request.PublicId)
            .Select(p => new HabeasDataPolicyDetailDto(
                p.PublicId,
                p.VersionNumber,
                p.Title,
                p.ContentMarkdown,
                p.Sha256Hex,
                p.EffectiveFrom,
                p.EffectiveTo,
                p.PublishedBy))
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<HabeasDataPolicyDetailDto>("Generic.NotFound", "Versión no encontrada.")
            : Result.Success(dto);
    }
}

public sealed class ListHabeasDataHistoryQueryHandler
    : IRequestHandler<ListHabeasDataHistoryQuery, Result<IReadOnlyList<HabeasDataHistoryItemDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ListHabeasDataHistoryQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db; _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<HabeasDataHistoryItemDto>>> Handle(
        ListHabeasDataHistoryQuery request, CancellationToken ct)
    {
        if (!int.TryParse(_currentUser.TenantId, out var tenantId))
        {
            return Result.Failure<IReadOnlyList<HabeasDataHistoryItemDto>>(
                "Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");
        }

        var items = await _db.HabeasDataConsents
            .Where(c => c.TenantId == tenantId && c.PersonId == request.PersonId)
            .OrderByDescending(c => c.ActionAt)
            .Include(c => c.PolicyVersion)
            .Select(c => new HabeasDataHistoryItemDto(
                c.PublicId,
                c.PolicyVersion!.VersionNumber,
                c.PolicyVersion.PublicId,
                c.Action,
                c.ActionAt,
                c.ActionBy,
                c.Channel,
                c.Notes))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<HabeasDataHistoryItemDto>>(items);
    }
}
