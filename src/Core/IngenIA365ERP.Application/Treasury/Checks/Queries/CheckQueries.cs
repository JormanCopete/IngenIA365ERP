using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Checks.Queries;

// --- DTOs ---

public record CheckDto(
    Guid PublicId,
    string BankName,
    int CheckNumber,
    string PayeeName,
    decimal Amount,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Status,
    string StatusText);

public record CheckDetailDto(
    Guid PublicId,
    string BankName,
    int BankId,
    int CheckNumber,
    int SequentialNumber,
    string PayeeName,
    string? PayeeCode,
    string ConceptCode,
    decimal Amount,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Status,
    string StatusText,
    string? VoidDetail,
    DateTime? VoidDate,
    string? VoidUserId);

// --- Status helper ---

public static class CheckStatusHelper
{
    public static string GetStatusText(string? code) => code switch
    {
        "E" => "Emitido",
        "C" => "Cobrado",
        "A" => "Anulado",
        "D" => "Devuelto",
        _ => code ?? "Desconocido"
    };
}

// --- List Checks Query ---

public record ListChecksQuery : IRequest<Result<PagedList<CheckDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? BankPublicId { get; init; }
    public string? Status { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class ListChecksQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListChecksQuery, Result<PagedList<CheckDto>>>
{
    public async Task<Result<PagedList<CheckDto>>> Handle(
        ListChecksQuery request, CancellationToken ct)
    {
        var query = context.Checks.AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(c => c.Status == request.Status);

        if (request.BankPublicId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.BankPublicId.Value && !b.IsDeleted, ct);
            if (bank is not null)
                query = query.Where(c => c.BankId == bank.Id);
        }

        if (request.DateFrom.HasValue)
            query = query.Where(c => c.CheckDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(c => c.CheckDate <= request.DateTo.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CheckDate)
            .ThenByDescending(c => c.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new
            {
                c.PublicId,
                c.BankId,
                c.CheckNumber,
                c.PersonId,
                c.Amount,
                c.CheckDate,
                c.Status
            })
            .ToListAsync(ct);

        // Batch load bank and person names
        var bankIds = items.Select(i => i.BankId).Distinct().ToList();
        var bankNames = await context.Banks.AsNoTracking()
            .Where(b => bankIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var personIds = items.Where(i => i.PersonId.HasValue).Select(i => i.PersonId!.Value).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);

        var dtos = items.Select(c => new CheckDto(
            c.PublicId,
            bankNames.TryGetValue(c.BankId, out var bName) ? bName : $"Banco:{c.BankId}",
            c.CheckNumber,
            c.PersonId.HasValue && personNames.TryGetValue(c.PersonId.Value, out var pName) ? pName : "Sin beneficiario",
            c.Amount,
            c.CheckDate,
            null, // DueDate not stored directly on Check entity
            c.Status ?? "",
            CheckStatusHelper.GetStatusText(c.Status)
        )).ToList();

        return Result.Success(new PagedList<CheckDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Check By Id Query ---

public record GetCheckByIdQuery(Guid PublicId) : IRequest<Result<CheckDetailDto>>;

public class GetCheckByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCheckByIdQuery, Result<CheckDetailDto>>
{
    public async Task<Result<CheckDetailDto>> Handle(
        GetCheckByIdQuery request, CancellationToken ct)
    {
        var check = await context.Checks.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (check is null)
            return Result.Failure<CheckDetailDto>(new Error("Check.NotFound",
                "Cheque no encontrado."));

        var bank = await context.Banks.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == check.BankId, ct);
        var bankName = bank?.Name ?? $"Banco:{check.BankId}";

        string payeeName = "Sin beneficiario";
        string? payeeCode = null;
        if (check.PersonId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == check.PersonId.Value, ct);
            if (person is not null)
            {
                payeeName = $"{person.FirstName} {person.LastName}";
                payeeCode = person.LegacyCode ?? person.TaxId;
            }
        }

        return Result.Success(new CheckDetailDto(
            check.PublicId,
            bankName,
            check.BankId,
            check.CheckNumber,
            check.SequentialNumber,
            payeeName,
            payeeCode,
            check.ConceptCode,
            check.Amount,
            check.CheckDate,
            null,
            check.Status ?? "",
            CheckStatusHelper.GetStatusText(check.Status),
            check.VoidDetail,
            check.VoidDate,
            check.VoidUserId));
    }
}
