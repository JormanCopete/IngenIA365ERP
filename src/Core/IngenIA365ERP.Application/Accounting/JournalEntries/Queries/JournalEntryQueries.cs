using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.JournalEntries.Queries;

// --- DTOs ---

public record JournalEntryQueryDto(
    Guid PublicId,
    string VoucherTypeCode,
    long DocumentNumber,
    string AccountCode,
    string AccountName,
    string? PersonName,
    string? BranchName,
    string? CostCenterName,
    DateOnly TransactionDate,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    int Status);

// --- List Journal Entries ---

public record ListJournalEntriesQuery : IRequest<Result<PagedList<JournalEntryQueryDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public Guid? AccountPublicId { get; init; }
    public Guid? PersonPublicId { get; init; }
    public Guid? BranchPublicId { get; init; }
    public Guid? CostCenterPublicId { get; init; }
    public Guid? VoucherTypePublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class ListJournalEntriesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListJournalEntriesQuery, Result<PagedList<JournalEntryQueryDto>>>
{
    public async Task<Result<PagedList<JournalEntryQueryDto>>> Handle(
        ListJournalEntriesQuery request, CancellationToken ct)
    {
        var query = context.JournalEntries
            .AsNoTracking()
            .Where(j => !j.IsDeleted);

        // Resolve PublicId filters to internal Ids
        if (request.AccountPublicId.HasValue)
        {
            var account = await context.ChartOfAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.PublicId == request.AccountPublicId.Value && !a.IsDeleted, ct);
            if (account is not null)
                query = query.Where(j => j.AccountId == account.Id);
            else
                return Result.Success(new PagedList<JournalEntryQueryDto>([], 0, request.PageNumber, request.PageSize));
        }

        if (request.PersonPublicId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId.Value && !p.IsDeleted, ct);
            if (person is not null)
                query = query.Where(j => j.PersonId == person.Id);
            else
                return Result.Success(new PagedList<JournalEntryQueryDto>([], 0, request.PageNumber, request.PageSize));
        }

        if (request.BranchPublicId.HasValue)
        {
            var branch = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId.Value && !b.IsDeleted, ct);
            if (branch is not null)
                query = query.Where(j => j.BranchId == branch.Id);
            else
                return Result.Success(new PagedList<JournalEntryQueryDto>([], 0, request.PageNumber, request.PageSize));
        }

        if (request.CostCenterPublicId.HasValue)
        {
            var cc = await context.CostCenters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CostCenterPublicId.Value && !c.IsDeleted, ct);
            if (cc is not null)
                query = query.Where(j => j.CostCenterId == cc.Id);
            else
                return Result.Success(new PagedList<JournalEntryQueryDto>([], 0, request.PageNumber, request.PageSize));
        }

        if (request.VoucherTypePublicId.HasValue)
        {
            var vt = await context.VoucherTypes.AsNoTracking()
                .FirstOrDefaultAsync(v => v.PublicId == request.VoucherTypePublicId.Value && !v.IsDeleted, ct);
            if (vt is not null)
                query = query.Where(j => j.VoucherTypeCode == vt.Code);
        }

        if (request.DateFrom.HasValue)
            query = query.Where(j => j.TransactionDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(j => j.TransactionDate <= request.DateTo.Value);

        var totalCount = await query.CountAsync(ct);

        // Join with related tables for names
        var joinedQuery = query
            .Join(context.ChartOfAccounts.AsNoTracking(),
                j => j.AccountId, a => a.Id,
                (j, a) => new { Entry = j, Account = a });

        var rawItems = await joinedQuery
            .OrderByDescending(x => x.Entry.TransactionDate)
            .ThenByDescending(x => x.Entry.DocumentNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.Entry.PublicId,
                x.Entry.VoucherTypeCode,
                x.Entry.DocumentNumber,
                x.Account.AccountCode,
                AccountName = x.Account.Name,
                x.Entry.PersonId,
                x.Entry.BranchId,
                x.Entry.CostCenterId,
                x.Entry.TransactionDate,
                x.Entry.Description,
                x.Entry.DebitAmount,
                x.Entry.CreditAmount,
                x.Entry.Status
            })
            .ToListAsync(ct);

        // Batch-load names
        var personIds = rawItems.Where(r => r.PersonId.HasValue).Select(r => r.PersonId!.Value).Distinct().ToList();
        var branchIds = rawItems.Select(r => r.BranchId).Distinct().ToList();
        var ccIds = rawItems.Select(r => r.CostCenterId).Distinct().ToList();

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

        var items = rawItems.Select(r => new JournalEntryQueryDto(
            r.PublicId,
            r.VoucherTypeCode,
            r.DocumentNumber,
            r.AccountCode,
            r.AccountName,
            r.PersonId.HasValue && personNames.TryGetValue(r.PersonId.Value, out var pn) ? pn : null,
            branchNames.TryGetValue(r.BranchId, out var bn) ? bn : null,
            ccNames.TryGetValue(r.CostCenterId, out var cn) ? cn : null,
            r.TransactionDate,
            r.Description,
            r.DebitAmount,
            r.CreditAmount,
            r.Status
        )).ToList();

        return Result.Success(new PagedList<JournalEntryQueryDto>(items, totalCount, request.PageNumber, request.PageSize));
    }
}
