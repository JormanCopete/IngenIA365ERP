using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.PeriodClose.Commands.CloseAccountingPeriod;

// DTOs
public record PeriodCloseResultDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Status { get; init; } = string.Empty;
    public int DraftDocumentsClosed { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public bool YearEndClosingGenerated { get; init; }
    public Guid? ClosingDocumentId { get; init; }
}

// Command
public record CloseAccountingPeriodCommand(int Year, int Month) : IRequest<Result<PeriodCloseResultDto>>;

// Handler
public class CloseAccountingPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CloseAccountingPeriodCommand, Result<PeriodCloseResultDto>>
{
    public async Task<Result<PeriodCloseResultDto>> Handle(
        CloseAccountingPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var periodCode = request.Year * 100 + request.Month;

        var period = await context.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.ModuleCode == "CNT" &&
                p.Year == request.Year &&
                p.PeriodNumber == request.Month &&
                !p.IsDeleted,
                cancellationToken);

        if (period is null)
            return Result.Failure<PeriodCloseResultDto>(new Error("PeriodClose.NotFound", "Periodo contable no encontrado."));

        if (period.Status == "C")
            return Result.Failure<PeriodCloseResultDto>(new Error("PeriodClose.AlreadyClosed", "El periodo ya se encuentra cerrado."));

        // Check for draft documents
        var draftCount = await context.AccountingDocuments
            .AsNoTracking()
            .CountAsync(d =>
                d.PeriodCode == periodCode &&
                !d.IsClosed &&
                !d.IsVoided &&
                !d.IsDeleted,
                cancellationToken);

        if (draftCount > 0)
            return Result.Failure<PeriodCloseResultDto>(new Error("PeriodClose.DraftDocuments",
                $"Existen {draftCount} comprobante(s) en borrador. Debe contabilizarlos o anularlos antes de cerrar."));

        // Validate global balance
        var balanceSums = await context.JournalEntries
            .AsNoTracking()
            .Where(j => j.PeriodCode == periodCode.ToString() && !j.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalDebit = g.Sum(j => j.DebitAmount),
                TotalCredit = g.Sum(j => j.CreditAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalDebit = balanceSums?.TotalDebit ?? 0m;
        var totalCredit = balanceSums?.TotalCredit ?? 0m;

        if (totalDebit != totalCredit)
            return Result.Failure<PeriodCloseResultDto>(new Error("PeriodClose.Unbalanced",
                $"Los movimientos del periodo no cuadran. Debitos: {totalDebit:N2}, Creditos: {totalCredit:N2}, Diferencia: {totalDebit - totalCredit:N2}"));

        // Year-end closing entry (month 12)
        Guid? closingDocumentId = null;
        var yearEndGenerated = false;

        if (request.Month == 12)
        {
            // Query result accounts (classes 4, 5, 6, 7)
            var resultAccounts = await context.ChartOfAccounts
                .AsNoTracking()
                .Where(a => !a.IsDeleted &&
                    (a.AccountCode.StartsWith("4") ||
                     a.AccountCode.StartsWith("5") ||
                     a.AccountCode.StartsWith("6") ||
                     a.AccountCode.StartsWith("7")))
                .Select(a => new { a.Id, a.AccountCode, a.Nature })
                .ToListAsync(cancellationToken);

            if (resultAccounts.Count > 0)
            {
                var accountIds = resultAccounts.Select(a => a.Id).ToList();

                // Sum balances for result accounts in this year
                var resultBalances = await context.AccountBalances
                    .AsNoTracking()
                    .Where(b => accountIds.Contains(b.AccountId) &&
                                b.PeriodYear == request.Year &&
                                !b.IsDeleted)
                    .GroupBy(b => b.AccountId)
                    .Select(g => new
                    {
                        AccountId = g.Key,
                        Debit = g.Sum(b => b.DebitAmount),
                        Credit = g.Sum(b => b.CreditAmount)
                    })
                    .ToListAsync(cancellationToken);

                var netResult = resultBalances.Sum(b => b.Credit - b.Debit);

                if (netResult != 0)
                {
                    // Find equity account (class 3 - Resultado del Ejercicio)
                    var equityAccount = await context.ChartOfAccounts
                        .AsNoTracking()
                        .Where(a => a.AccountCode.StartsWith("36") && a.Level > 3 && !a.IsDeleted)
                        .OrderBy(a => a.AccountCode)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (equityAccount is not null)
                    {
                        var closingDoc = new AccountingDocument
                        {
                            VoucherTypeCode = "CIE",
                            DocumentNumber = 1,
                            Detail = $"Cierre de cuentas de resultado - Ano {request.Year}",
                            TotalDebit = Math.Abs(netResult),
                            TotalCredit = Math.Abs(netResult),
                            DocumentDate = new DateOnly(request.Year, 12, 31),
                            IsClosed = true,
                            PeriodCode = periodCode,
                            ModuleCode = "CNT",
                            CreatedAt = dateTime.UtcNow,
                            CreatedBy = currentUser.UserName
                        };

                        context.AccountingDocuments.Add(closingDoc);
                        closingDocumentId = closingDoc.PublicId;
                        yearEndGenerated = true;
                    }
                }
            }
        }

        // Close the period
        period.Status = "C";
        period.UpdatedAt = dateTime.UtcNow;
        period.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new PeriodCloseResultDto
        {
            Year = request.Year,
            Month = request.Month,
            Status = "C",
            DraftDocumentsClosed = 0,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            YearEndClosingGenerated = yearEndGenerated,
            ClosingDocumentId = closingDocumentId
        });
    }
}

// Validator
public class CloseAccountingPeriodCommandValidator : AbstractValidator<CloseAccountingPeriodCommand>
{
    public CloseAccountingPeriodCommandValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000).WithMessage("El ano debe ser mayor a 2000.");
        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("El mes debe estar entre 1 y 12.");
    }
}
