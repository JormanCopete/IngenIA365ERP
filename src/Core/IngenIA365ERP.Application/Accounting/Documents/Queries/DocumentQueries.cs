using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents.Queries;

// --- DTOs ---

public record DocumentDto(
    Guid PublicId,
    string VoucherTypeCode,
    long DocumentNumber,
    DateOnly DocumentDate,
    string? Description,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsClosed,
    bool IsVoided,
    int? PeriodCode);

public record JournalEntryLineResultDto(
    Guid PublicId,
    string AccountCode,
    string AccountName,
    string? PersonName,
    string? BranchName,
    string? CostCenterName,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    string? Reference);

public record DocumentDetailDto(
    Guid PublicId,
    string VoucherTypeCode,
    string VoucherTypeName,
    long DocumentNumber,
    DateOnly DocumentDate,
    string? Description,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsClosed,
    bool IsVoided,
    int? PeriodCode,
    List<JournalEntryLineResultDto> Lines);

// --- List Documents ---

public record ListDocumentsQuery : IRequest<Result<PagedList<DocumentDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? VoucherTypePublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public bool? IsClosed { get; init; }
    public bool? IsVoided { get; init; }
}

public class ListDocumentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDocumentsQuery, Result<PagedList<DocumentDto>>>
{
    public async Task<Result<PagedList<DocumentDto>>> Handle(
        ListDocumentsQuery request, CancellationToken ct)
    {
        var query = context.AccountingDocuments
            .AsNoTracking()
            .Where(d => !d.IsDeleted);

        // Filters
        if (request.VoucherTypePublicId.HasValue)
        {
            var vt = await context.VoucherTypes.AsNoTracking()
                .FirstOrDefaultAsync(v => v.PublicId == request.VoucherTypePublicId.Value && !v.IsDeleted, ct);
            if (vt is not null)
                query = query.Where(d => d.VoucherTypeCode == vt.Code);
        }
        if (request.DateFrom.HasValue)
            query = query.Where(d => d.DocumentDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(d => d.DocumentDate <= request.DateTo.Value);
        if (request.IsClosed.HasValue)
            query = query.Where(d => d.IsClosed == request.IsClosed.Value);
        if (request.IsVoided.HasValue)
            query = query.Where(d => d.IsVoided == request.IsVoided.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.DocumentDate)
            .ThenByDescending(d => d.DocumentNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DocumentDto(
                d.PublicId,
                d.VoucherTypeCode,
                d.DocumentNumber,
                d.DocumentDate,
                d.Detail,
                d.TotalDebit,
                d.TotalCredit,
                d.IsClosed,
                d.IsVoided,
                d.PeriodCode))
            .ToListAsync(ct);

        return Result.Success(new PagedList<DocumentDto>(items, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Document By Id ---

public record GetDocumentByIdQuery(Guid PublicId) : IRequest<Result<DocumentDetailDto>>;

public class GetDocumentByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDocumentByIdQuery, Result<DocumentDetailDto>>
{
    public async Task<Result<DocumentDetailDto>> Handle(
        GetDocumentByIdQuery request, CancellationToken ct)
    {
        var document = await context.AccountingDocuments
            .AsNoTracking()
            .Include(d => d.VoucherType)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);

        if (document is null)
            return Result.Failure<DocumentDetailDto>(new Error("Document.NotFound",
                "Comprobante no encontrado."));

        // Get journal entry lines with related names via join
        var lines = await context.JournalEntries
            .AsNoTracking()
            .Where(j => j.VoucherTypeCode == document.VoucherTypeCode
                     && j.DocumentNumber == document.DocumentNumber
                     && !j.IsDeleted)
            .Join(context.ChartOfAccounts.AsNoTracking(),
                j => j.AccountId, a => a.Id,
                (j, a) => new { Entry = j, Account = a })
            .Select(x => new
            {
                x.Entry.PublicId,
                x.Account.AccountCode,
                AccountName = x.Account.Name,
                x.Entry.PersonId,
                x.Entry.BranchId,
                x.Entry.CostCenterId,
                x.Entry.DebitAmount,
                x.Entry.CreditAmount,
                x.Entry.Description,
                x.Entry.AuxiliaryDocument
            })
            .ToListAsync(ct);

        // Batch-load person, branch, cost center names
        var personIds = lines.Where(l => l.PersonId.HasValue).Select(l => l.PersonId!.Value).Distinct().ToList();
        var branchIds = lines.Select(l => l.BranchId).Distinct().ToList();
        var ccIds = lines.Select(l => l.CostCenterId).Distinct().ToList();

        var personNames = personIds.Count > 0
            ? await context.People.AsNoTracking()
                .Where(p => personIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct)
            : new Dictionary<int, string>();

        var branchNames = branchIds.Count > 0
            ? await context.Branches.AsNoTracking()
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Name, ct)
            : new Dictionary<int, string>();

        var ccNames = ccIds.Count > 0
            ? await context.CostCenters.AsNoTracking()
                .Where(c => ccIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<int, string>();

        var lineDtos = lines.Select(l => new JournalEntryLineResultDto(
            l.PublicId,
            l.AccountCode,
            l.AccountName,
            l.PersonId.HasValue && personNames.TryGetValue(l.PersonId.Value, out var pn) ? pn : null,
            branchNames.TryGetValue(l.BranchId, out var bn) ? bn : null,
            ccNames.TryGetValue(l.CostCenterId, out var cn) ? cn : null,
            l.DebitAmount,
            l.CreditAmount,
            l.Description,
            l.AuxiliaryDocument
        )).ToList();

        var dto = new DocumentDetailDto(
            document.PublicId,
            document.VoucherTypeCode,
            document.VoucherType?.Name ?? "",
            document.DocumentNumber,
            document.DocumentDate,
            document.Detail,
            document.TotalDebit,
            document.TotalCredit,
            document.IsClosed,
            document.IsVoided,
            document.PeriodCode,
            lineDtos);

        return Result.Success(dto);
    }
}
