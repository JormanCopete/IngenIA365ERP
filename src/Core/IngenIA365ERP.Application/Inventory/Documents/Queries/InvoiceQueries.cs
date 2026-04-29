using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Queries;

// --- DTOs ---

public record InvoiceDto(
    Guid PublicId,
    long InvoiceNumber,
    DateOnly Date,
    string CustomerName,
    string CustomerTaxId,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal VatAmount,
    bool IsVoided);

public record InvoiceDetailDto(
    Guid PublicId,
    long InvoiceNumber,
    DateOnly Date,
    string CustomerName,
    string CustomerTaxId,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal VatAmount,
    string? Detail,
    bool IsVoided,
    List<InvoiceLineResultDto> Lines);

public record InvoiceLineResultDto(
    string ProductName,
    int ProductCode,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal SubTotal,
    decimal NetTotal);

// --- List Invoices ---

public record ListInvoicesQuery : IRequest<Result<PagedList<InvoiceDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? CustomerSearch { get; init; }
}

public class ListInvoicesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListInvoicesQuery, Result<PagedList<InvoiceDto>>>
{
    public async Task<Result<PagedList<InvoiceDto>>> Handle(
        ListInvoicesQuery request, CancellationToken ct)
    {
        var query = context.InventoryDocuments
            .AsNoTracking()
            .Where(d => !d.IsDeleted && d.InvoiceNumber.HasValue && d.InvoiceNumber > 0);

        if (request.DateFrom.HasValue)
        {
            var from = request.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(d => d.EntryDate >= from);
        }
        if (request.DateTo.HasValue)
        {
            var to = request.DateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(d => d.EntryDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerSearch))
        {
            var search = request.CustomerSearch.Trim();
            var matchingCustomerIds = await context.People.AsNoTracking()
                .Where(p => !p.IsDeleted &&
                    (p.TaxId.Contains(search) || p.FirstName.Contains(search) || p.LastName.Contains(search)))
                .Select(p => (int?)p.Id)
                .ToListAsync(ct);
            query = query.Where(d => d.CustomerId.HasValue && matchingCustomerIds.Contains(d.CustomerId));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.InvoiceNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Batch load customer names
        var customerIds = items.Where(d => d.CustomerId.HasValue).Select(d => d.CustomerId!.Value).Distinct().ToList();
        var customerMap = new Dictionary<int, (string Name, string TaxId)>();
        if (customerIds.Count > 0)
        {
            var people = await context.People.AsNoTracking()
                .Where(p => customerIds.Contains(p.Id))
                .Select(p => new { p.Id, Name = p.FirstName + " " + p.LastName, p.TaxId })
                .ToListAsync(ct);
            foreach (var p in people)
                customerMap[p.Id] = (p.Name, p.TaxId);
        }

        var dtos = items.Select(d =>
        {
            string? custName = null;
            string? custTaxId = null;
            if (d.CustomerId.HasValue && customerMap.TryGetValue(d.CustomerId.Value, out var c))
            {
                custName = c.Name;
                custTaxId = c.TaxId;
            }
            return new InvoiceDto(
                d.PublicId,
                d.InvoiceNumber!.Value,
                DateOnly.FromDateTime(d.EntryDate),
                custName ?? "",
                custTaxId ?? "",
                d.TotalAmount,
                d.DiscountAmount,
                d.VatAmount,
                d.Status == -1);
        }).ToList();

        return Result.Success(new PagedList<InvoiceDto>(
            dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Invoice By Id ---

public record GetInvoiceByIdQuery(Guid PublicId) : IRequest<Result<InvoiceDetailDto>>;

public class GetInvoiceByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(
        GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var document = await context.InventoryDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted
                && d.InvoiceNumber.HasValue, ct);

        if (document is null)
            return Result.Failure<InvoiceDetailDto>(new Error("Invoice.NotFound",
                "Factura no encontrada."));

        // Customer info
        string customerName = "", customerTaxId = "";
        if (document.CustomerId.HasValue)
        {
            var customer = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == document.CustomerId.Value, ct);
            if (customer is not null)
            {
                customerName = $"{customer.FirstName} {customer.LastName}";
                customerTaxId = customer.TaxId;
            }
        }

        // Transaction lines
        var transactions = await context.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Where(t => t.TransactionTypeId == document.TransactionTypeId
                     && t.SequenceNumber == document.SequenceNumber
                     && !t.IsDeleted)
            .ToListAsync(ct);

        var lines = transactions.Select(t => new InvoiceLineResultDto(
            t.Product?.Name ?? "",
            t.Product?.ProductCode ?? 0,
            Math.Abs(t.Quantity),
            t.UnitPrice,
            t.DiscountRate,
            t.DiscountAmount,
            t.VatRate,
            t.VatAmount,
            t.SubTotal,
            t.NetTotal
        )).ToList();

        return Result.Success(new InvoiceDetailDto(
            document.PublicId,
            document.InvoiceNumber!.Value,
            DateOnly.FromDateTime(document.EntryDate),
            customerName,
            customerTaxId,
            document.TotalAmount,
            document.DiscountAmount,
            document.VatAmount,
            document.Detail,
            document.Status == -1,
            lines));
    }
}
