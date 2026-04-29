using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.Cards.Queries;

// --- DTOs ---

public record DebitCardDto(
    Guid PublicId,
    string MaskedCardNumber,
    string PersonName,
    string? AccountNumber,
    decimal Balance,
    string Status,
    string StatusText);

public record DebitCardDetailDto(
    Guid PublicId,
    string MaskedCardNumber,
    string PersonName,
    string? PersonCode,
    string? AccountNumber,
    decimal Balance,
    string Status,
    string StatusText,
    string CardType,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    decimal? DailyAtmLimit,
    decimal? DailyPosLimit,
    List<DebitTransactionDto> RecentTransactions);

public record DebitTransactionDto(
    Guid PublicId,
    DateTime? Date,
    string? Type,
    string TypeText,
    decimal Amount,
    string? MerchantCode,
    string? Status,
    string StatusText);

// --- Status helpers ---

public static class DebitCardStatusHelper
{
    public static string GetStatusText(string? code) => code switch
    {
        "A" => "Activa",
        "B" => "Bloqueada temporal",
        "D" => "Bloqueada definitiva",
        "I" => "Inactiva",
        _ => code ?? "Desconocido"
    };

    public static string GetTransactionTypeText(string? code) => code switch
    {
        "P" => "Compra POS",
        "A" => "Retiro cajero",
        "T" => "Transferencia",
        _ => code ?? "Otro"
    };

    public static string GetTransactionStatusText(string? code) => code switch
    {
        "A" => "Aprobada",
        "R" => "Rechazada",
        "P" => "Pendiente",
        "V" => "Anulada",
        _ => code ?? "Desconocido"
    };
}

// --- List Cards Query ---

public record ListDebitCardsQuery : IRequest<Result<PagedList<DebitCardDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? PersonPublicId { get; init; }
    public string? Status { get; init; }
}

public class ListDebitCardsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDebitCardsQuery, Result<PagedList<DebitCardDto>>>
{
    public async Task<Result<PagedList<DebitCardDto>>> Handle(
        ListDebitCardsQuery request, CancellationToken ct)
    {
        var query = context.DebitCards.AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(c => c.Status == request.Status);

        if (request.PersonPublicId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId.Value && !p.IsDeleted, ct);
            if (person is not null)
                query = query.Where(c => c.PersonId == person.Id);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.IssueDate)
            .ThenByDescending(c => c.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new
            {
                c.PublicId,
                c.CardNumber,
                c.PersonId,
                c.AccountNumber,
                c.AvailableBalance,
                c.Status
            })
            .ToListAsync(ct);

        // Batch load person names
        var personIds = items.Where(i => i.PersonId.HasValue).Select(i => i.PersonId!.Value).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);

        var dtos = items.Select(c => new DebitCardDto(
            c.PublicId,
            c.CardNumber,
            c.PersonId.HasValue && personNames.TryGetValue(c.PersonId.Value, out var name) ? name : "Sin asignar",
            c.AccountNumber?.ToString(),
            c.AvailableBalance ?? 0,
            c.Status ?? "I",
            DebitCardStatusHelper.GetStatusText(c.Status)
        )).ToList();

        return Result.Success(new PagedList<DebitCardDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Card By Id Query ---

public record GetDebitCardByIdQuery(Guid PublicId) : IRequest<Result<DebitCardDetailDto>>;

public class GetDebitCardByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDebitCardByIdQuery, Result<DebitCardDetailDto>>
{
    public async Task<Result<DebitCardDetailDto>> Handle(
        GetDebitCardByIdQuery request, CancellationToken ct)
    {
        var card = await context.DebitCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (card is null)
            return Result.Failure<DebitCardDetailDto>(new Error("DebitCard.NotFound",
                "Tarjeta debito no encontrada."));

        // Person
        string personName = "Sin asignar";
        string? personCode = null;
        if (card.PersonId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == card.PersonId.Value, ct);
            if (person is not null)
            {
                personName = $"{person.FirstName} {person.LastName}";
                personCode = person.LegacyCode ?? person.TaxId;
            }
        }

        // Recent transactions (last 50)
        var transactions = await context.DebitTransactions.AsNoTracking()
            .Where(t => t.CardId == card.Id && !t.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Take(50)
            .Select(t => new DebitTransactionDto(
                t.PublicId,
                t.TransactionDate,
                t.TransactionType,
                DebitCardStatusHelper.GetTransactionTypeText(t.TransactionType),
                t.Amount ?? 0,
                t.MerchantCode,
                t.Status,
                DebitCardStatusHelper.GetTransactionStatusText(t.Status)))
            .ToListAsync(ct);

        return Result.Success(new DebitCardDetailDto(
            card.PublicId,
            card.CardNumber,
            personName,
            personCode,
            card.AccountNumber?.ToString(),
            card.AvailableBalance ?? 0,
            card.Status ?? "I",
            DebitCardStatusHelper.GetStatusText(card.Status),
            card.IsDebitOrCredit,
            card.IssueDate,
            card.ExpiryDate,
            card.DailyAtmLimit,
            card.DailyPosLimit,
            transactions));
    }
}

// --- Get Card Transactions Query ---

public record GetDebitCardTransactionsQuery : IRequest<Result<PagedList<DebitTransactionDto>>>
{
    public Guid CardPublicId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class GetDebitCardTransactionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDebitCardTransactionsQuery, Result<PagedList<DebitTransactionDto>>>
{
    public async Task<Result<PagedList<DebitTransactionDto>>> Handle(
        GetDebitCardTransactionsQuery request, CancellationToken ct)
    {
        var card = await context.DebitCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.CardPublicId && !c.IsDeleted, ct);

        if (card is null)
            return Result.Failure<PagedList<DebitTransactionDto>>(new Error("DebitCard.NotFound",
                "Tarjeta debito no encontrada."));

        var query = context.DebitTransactions.AsNoTracking()
            .Where(t => t.CardId == card.Id && !t.IsDeleted);

        if (request.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate.HasValue
                                     && DateOnly.FromDateTime(t.TransactionDate.Value) >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate.HasValue
                                     && DateOnly.FromDateTime(t.TransactionDate.Value) <= request.DateTo.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new DebitTransactionDto(
                t.PublicId,
                t.TransactionDate,
                t.TransactionType,
                DebitCardStatusHelper.GetTransactionTypeText(t.TransactionType),
                t.Amount ?? 0,
                t.MerchantCode,
                t.Status,
                DebitCardStatusHelper.GetTransactionStatusText(t.Status)))
            .ToListAsync(ct);

        return Result.Success(new PagedList<DebitTransactionDto>(items, totalCount, request.PageNumber, request.PageSize));
    }
}
