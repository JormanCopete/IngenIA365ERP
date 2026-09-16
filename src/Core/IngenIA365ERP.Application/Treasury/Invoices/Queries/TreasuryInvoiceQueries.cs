using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Invoices.Queries;

// --- DTOs ---

public record TreasuryInvoiceDto(
    Guid PublicId,
    string InvoiceNumber,
    string DocumentType,
    string DocumentTypeText,
    string PersonName,
    decimal Amount,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Status,
    string StatusText,
    string? Description);

public record CashFlowDto(
    int Year,
    int Month,
    string Period,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetFlow);

// --- Status helper ---

public static class TreasuryInvoiceStatusHelper
{
    public static string GetStatusText(string? code) => code switch
    {
        "P" => "Pendiente",
        "G" => "Pagada",
        "A" => "Anulada",
        _ => code ?? "Desconocido"
    };

    public static string GetDocTypeText(string? code) => code switch
    {
        "CXP" => "Cuenta por Pagar",
        "CXC" => "Cuenta por Cobrar",
        _ => code ?? "Otro"
    };
}

// --- List Treasury Invoices Query ---

public record ListTreasuryInvoicesQuery : IRequest<Result<PagedList<TreasuryInvoiceDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? DocumentType { get; init; } // CXP or CXC
    public DateOnly? DueDateFrom { get; init; }
    public DateOnly? DueDateTo { get; init; }
    public string? Status { get; init; }
}

public class ListTreasuryInvoicesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListTreasuryInvoicesQuery, Result<PagedList<TreasuryInvoiceDto>>>
{
    public async Task<Result<PagedList<TreasuryInvoiceDto>>> Handle(
        ListTreasuryInvoicesQuery request, CancellationToken ct)
    {
        var query = context.TreasuryInvoices.AsNoTracking()
            .Where(i => !i.IsDeleted);

        if (!string.IsNullOrEmpty(request.DocumentType))
            query = query.Where(i => i.DocumentType == request.DocumentType);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(i => i.Status == request.Status);

        if (request.DueDateFrom.HasValue)
            query = query.Where(i => i.DueDate.HasValue && i.DueDate.Value >= request.DueDateFrom.Value);

        if (request.DueDateTo.HasValue)
            query = query.Where(i => i.DueDate.HasValue && i.DueDate.Value <= request.DueDateTo.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new
            {
                i.PublicId,
                i.InvoiceNumber,
                i.DocumentType,
                i.PersonId,
                i.Amount,
                i.InvoiceDate,
                i.DueDate,
                i.Status,
                i.Description
            })
            .ToListAsync(ct);

        // Batch load person names
        var personIds = items.Select(i => i.PersonId).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);

        var dtos = items.Select(i => new TreasuryInvoiceDto(
            i.PublicId,
            i.InvoiceNumber,
            i.DocumentType ?? "",
            TreasuryInvoiceStatusHelper.GetDocTypeText(i.DocumentType),
            personNames.TryGetValue(i.PersonId, out var name) ? name : $"ID:{i.PersonId}",
            i.Amount,
            i.InvoiceDate,
            i.DueDate,
            i.Status ?? "",
            TreasuryInvoiceStatusHelper.GetStatusText(i.Status),
            i.Description
        )).ToList();

        return Result.Success(new PagedList<TreasuryInvoiceDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Cash Flow Query ---

public record GetCashFlowQuery : IRequest<Result<List<CashFlowDto>>>
{
    public int Year { get; init; }
    public int? MonthFrom { get; init; }
    public int? MonthTo { get; init; }
}

public class GetCashFlowQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCashFlowQuery, Result<List<CashFlowDto>>>
{
    public async Task<Result<List<CashFlowDto>>> Handle(
        GetCashFlowQuery request, CancellationToken ct)
    {
        var monthFrom = request.MonthFrom ?? 1;
        var monthTo = request.MonthTo ?? 12;

        // E3 (feature 009): contabilización por AccountingPoster pendiente: los saldos se derivan de ACC_JournalEntries (R4); la tabla de saldos ya no existe.
        var balances = new[] { new { Month = 0, TotalDebit = 0m, TotalCredit = 0m } }.Where(_ => false).ToList();

        var monthNames = new[]
        {
            "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
            "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
        };

        var results = new List<CashFlowDto>();

        for (var m = monthFrom; m <= monthTo; m++)
        {
            var balance = balances.FirstOrDefault(b => b.Month == m);
            var income = balance?.TotalCredit ?? 0;
            var expense = balance?.TotalDebit ?? 0;

            results.Add(new CashFlowDto(
                request.Year,
                m,
                $"{monthNames[m]} {request.Year}",
                income,
                expense,
                income - expense));
        }

        return Result.Success(results);
    }
}
