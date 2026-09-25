using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments.Queries;

// --- DTOs ---

public record PaymentReceiptDto(
    Guid TransactionPublicId,
    long DocumentNumber,
    DateOnly PaymentDate,
    string PersonName,
    string IdentificationNumber,
    long PortfolioNumber,
    string CreditLineName,
    decimal PaidCapital,
    decimal PaidInterest,
    decimal PaidDefault,
    decimal TotalPaid,
    decimal RemainingBalance,
    string PaymentMethod,
    string? Reference,
    string ReceivedBy);

// --- Get Payment Receipt ---

public record GetPaymentReceiptQuery(Guid TransactionPublicId) : IRequest<Result<PaymentReceiptDto>>;

public class GetPaymentReceiptQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPaymentReceiptQuery, Result<PaymentReceiptDto>>
{
    public async Task<Result<PaymentReceiptDto>> Handle(
        GetPaymentReceiptQuery request, CancellationToken ct)
    {
        var transaction = await context.LendingTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == request.TransactionPublicId && !t.IsDeleted, ct);

        if (transaction is null)
            return Result.Failure<PaymentReceiptDto>(new Error("Payment.NotFound",
                "Transaccion no encontrada."));

        // Get portfolio info
        var portfolio = await context.LoanPortfolios
            .AsNoTracking()
            .Include(lp => lp.Person)
            .Include(lp => lp.CreditLine)
            .FirstOrDefaultAsync(lp => lp.PortfolioNumber == transaction.PortfolioNumber
                                    && lp.CreditLineId == transaction.CreditLineId
                                    && !lp.IsDeleted, ct);

        var personName = portfolio?.Person is not null
            ? NombreDePersona.Completo(portfolio.Person)
            : transaction.PersonCode;

        // Parse description for capital/interest/default breakdown
        // Description format: "Recaudo credito #X - Cap:N Int:N Mora:N"
        decimal paidCapital = 0, paidInterest = 0, paidDefault = 0;
        var desc = transaction.Description ?? "";
        if (desc.Contains("Cap:"))
        {
            var parts = desc.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length > 1)
            {
                var breakdown = parts[^1];
                foreach (var part in breakdown.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.StartsWith("Cap:"))
                        decimal.TryParse(part[4..].Replace(",", "").Replace(".", ""), out paidCapital);
                    else if (part.StartsWith("Int:"))
                        decimal.TryParse(part[4..].Replace(",", "").Replace(".", ""), out paidInterest);
                    else if (part.StartsWith("Mora:"))
                        decimal.TryParse(part[5..].Replace(",", "").Replace(".", ""), out paidDefault);
                }
            }
        }

        // If parsing fails, use total
        var totalPaid = transaction.CreditAmount > 0 ? transaction.CreditAmount : transaction.DebitAmount;
        if (paidCapital + paidInterest + paidDefault == 0)
            paidCapital = totalPaid;

        var receipt = new PaymentReceiptDto(
            transaction.PublicId,
            transaction.DocumentNumber,
            transaction.TransactionDate,
            personName,
            transaction.IdentificationNumber ?? "",
            transaction.PortfolioNumber,
            portfolio?.CreditLine?.Description ?? "",
            paidCapital,
            paidInterest,
            paidDefault,
            totalPaid,
            portfolio?.CurrentBalance ?? 0,
            transaction.TransactionCode,
            transaction.CrossDocumentNumber,
            transaction.UserId ?? "");

        return Result.Success(receipt);
    }
}
