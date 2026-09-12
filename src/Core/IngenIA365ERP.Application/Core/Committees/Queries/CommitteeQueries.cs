using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Committees.Queries;

public record CommitteeDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CommitteeType { get; init; }
}

public record ListCommitteesQuery : IRequest<Result<PagedList<CommitteeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCommitteesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCommitteesQuery, Result<PagedList<CommitteeDto>>>
{
    public async Task<Result<PagedList<CommitteeDto>>> Handle(
        ListCommitteesQuery request, CancellationToken ct)
    {
        var query = context.Committees
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.ShortName != null && c.ShortName.Contains(term)));
        }

        query = query.OrderBy(c => c.Name);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(c => new CommitteeDto
            {
                PublicId = c.PublicId,
                Code = c.LegacyCode,
                Name = c.Name,
                ShortName = c.ShortName,
                CommitteeType = c.CommitteeType
            })
            .ToListAsync(ct);

        return Result.Success(new PagedList<CommitteeDto>(
            items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize));
    }
}

public record GetCommitteeByIdQuery(Guid PublicId) : IRequest<Result<CommitteeDto>>;

public class GetCommitteeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCommitteeByIdQuery, Result<CommitteeDto>>
{
    public async Task<Result<CommitteeDto>> Handle(GetCommitteeByIdQuery request, CancellationToken ct)
    {
        var dto = await context.Committees
            .AsNoTracking()
            .Where(c => c.PublicId == request.PublicId && !c.IsDeleted)
            .Select(c => new CommitteeDto
            {
                PublicId = c.PublicId,
                Code = c.LegacyCode,
                Name = c.Name,
                ShortName = c.ShortName,
                CommitteeType = c.CommitteeType
            })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<CommitteeDto>(new Error("Committee.NotFound", "Comite no encontrado."))
            : Result.Success(dto);
    }
}
