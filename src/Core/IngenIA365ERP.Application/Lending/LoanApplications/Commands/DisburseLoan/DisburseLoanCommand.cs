using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.LoanApplications.Commands.DisburseLoan;

public record DisburseLoanCommand : IRequest<Result<DisbursementResultDto>>
{
    public Guid ApplicationPublicId { get; init; }
    public DateOnly DisbursementDate { get; init; }
    public string? BankAccountCode { get; init; }
    public string? VoucherTypeCode { get; init; }
}

public record DisbursementResultDto(
    Guid PortfolioPublicId,
    long PortfolioNumber,
    int InstallmentCount,
    decimal InstallmentAmount,
    decimal TotalInterest);

public class DisburseLoanCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DisburseLoanCommand, Result<DisbursementResultDto>>
{
    public async Task<Result<DisbursementResultDto>> Handle(DisburseLoanCommand request, CancellationToken ct)
    {
        // 1. Find LoanApplication and validate Status=="A"
        var application = await context.LoanApplications
            .Include(a => a.CreditLine)
            .FirstOrDefaultAsync(a => a.PublicId == request.ApplicationPublicId && !a.IsDeleted, ct);

        if (application is null)
            return Result.Failure<DisbursementResultDto>(new Error("Disburse.ApplicationNotFound",
                "Solicitud de credito no encontrada."));

        if (application.Status != "A")
            return Result.Failure<DisbursementResultDto>(new Error("Disburse.InvalidStatus",
                $"Solo se pueden desembolsar solicitudes aprobadas. Estado actual: {application.Status}"));

        var creditLine = application.CreditLine;
        if (creditLine is null)
        {
            creditLine = await context.CreditLineParameters
                .FirstOrDefaultAsync(cl => cl.Id == application.CreditLineId && !cl.IsDeleted, ct);
            if (creditLine is null)
                return Result.Failure<DisbursementResultDto>(new Error("Disburse.CreditLineNotFound",
                    "Linea de credito no encontrada."));
        }

        // 2. Resolve Person
        var person = await context.People
            .FirstOrDefaultAsync(p => p.TaxId == application.PersonCode || p.LegacyCode == application.PersonCode, ct);
        if (person is null)
            return Result.Failure<DisbursementResultDto>(new Error("Disburse.PersonNotFound",
                "Persona asociada a la solicitud no encontrada."));

        // 3. Generate portfolio number (auto-increment)
        var maxPortfolio = await context.LoanPortfolios
            .Where(lp => !lp.IsDeleted)
            .MaxAsync(lp => (long?)lp.PortfolioNumber, ct) ?? 0;
        var portfolioNumber = maxPortfolio + 1;

        var approvedAmount = application.ApprovedAmount;
        var rate = application.InterestRate;
        var term = application.Term;
        var monthlyRate = rate / 100m / 12m;

        // 4. Calculate French system installment
        decimal installmentAmount;
        if (monthlyRate > 0)
        {
            var factor = (double)monthlyRate * Math.Pow(1 + (double)monthlyRate, term)
                         / (Math.Pow(1 + (double)monthlyRate, term) - 1);
            installmentAmount = approvedAmount * (decimal)factor;
        }
        else
        {
            installmentAmount = approvedAmount / term;
        }
        installmentAmount = Math.Round(installmentAmount, 0);

        // 5. Create LoanPortfolio
        var portfolio = new LoanPortfolio
        {
            PersonId = person.Id,
            CreditLineId = application.CreditLineId,
            PortfolioNumber = portfolioNumber,
            IdentificationNumber = person.TaxId,
            ApplicationDate = application.ApplicationDate,
            ApprovalDate = application.ApprovalDate,
            DisbursementDate = request.DisbursementDate,
            DiscountStartDate = request.DisbursementDate.AddDays(30),
            TermMonths = term,
            RequestedAmount = application.RequestedAmount,
            ApprovedAmount = approvedAmount,
            CurrentBalance = approvedAmount,
            InstallmentAmount = installmentAmount,
            InterestRate = rate,
            PaymentCycle = application.PaymentCycle,
            PaymentPeriodicity = application.Periodicity,
            InstallmentType = application.InstallmentType.Length > 0 ? application.InstallmentType : "F",
            InterestType = application.InterestType.Length > 0 ? application.InterestType : "V",
            GuaranteeType = application.GuaranteeType,
            DeductionType = application.DeductionType.Length > 0 ? application.DeductionType : "N",
            PaidInstallments = 0,
            AdminFeeRate = creditLine.AdminRate,
            InsuranceRate = creditLine.InsuranceRate,
            DocumentType = "DS",
            DaysOverdue = 0,
            PendingInstallmentCount = term,
            ApplicationNumber = application.ApplicationNumber,
            BranchId = application.BranchId,
            CostCenterId = application.CostCenterId,
            Category = "A", // Initial classification
            Codeudor1 = application.Codeudor1,
            Codeudor2 = application.Codeudor2,
            Codeudor3 = application.Codeudor3,
            Codeudor4 = application.Codeudor4,
            MaturityDate = request.DisbursementDate.AddMonths(term),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.LoanPortfolios.Add(portfolio);

        // 6. Generate amortization table (PendingInstallments)
        var balance = approvedAmount;
        decimal totalInterest = 0m;

        for (int period = 1; period <= term; period++)
        {
            var interestPortion = Math.Round(balance * monthlyRate, 0);
            var capitalPortion = installmentAmount - interestPortion;

            // Adjust last installment for rounding
            if (period == term)
            {
                capitalPortion = balance;
                interestPortion = installmentAmount > balance ? installmentAmount - balance : interestPortion;
            }

            if (capitalPortion > balance)
                capitalPortion = balance;

            totalInterest += interestPortion;
            var processDate = request.DisbursementDate.AddMonths(period);
            var accrualPeriod = processDate.Year * 100 + processDate.Month;
            var accountingPeriod = accrualPeriod;

            var installment = new PendingInstallment
            {
                PersonCode = application.PersonCode,
                CreditLineId = application.CreditLineId,
                PortfolioNumber = portfolioNumber,
                AccrualPeriod = accrualPeriod,
                AccountingPeriod = accountingPeriod,
                CompanyCode = application.BranchId,
                CostCenterId = application.CostCenterId,
                Periodicity = application.Periodicity,
                Description = $"Cuota {period} de {term}",
                Cycle = period,
                TotalAmount = capitalPortion + interestPortion,
                AccruedInterest = interestPortion,
                AccruedCapital = capitalPortion,
                PaidInterest = 0,
                PaidCapital = 0,
                BalanceInterest = interestPortion,
                BalanceCapital = capitalPortion,
                CreditBalance = balance - capitalPortion,
                ObligationInstallment = installmentAmount,
                TotalInstallment = installmentAmount,
                InterestRate = rate,
                ProcessDate = processDate,
                UserId = currentUser.UserName ?? "system",
                SystemDate = dateTime.TodayUtc,
                EntryType = "G", // Generated
                TotalBalance = capitalPortion + interestPortion,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.PendingInstallments.Add(installment);

            balance -= capitalPortion;
            if (balance < 0) balance = 0;
        }

        // 7. Update LoanApplication Status="D" (Desembolsada)
        application.Status = "D";
        application.DisbursementDate = request.DisbursementDate;
        application.DisbursedAmount = approvedAmount;
        application.UpdatedAt = dateTime.UtcNow;
        application.UpdatedBy = currentUser.UserName;

        // 8. Create LendingTransaction (disbursement record)
        var voucherCode = request.VoucherTypeCode ?? "DS";
        var maxDocNum = await context.LendingTransactions
            .Where(t => t.VoucherType == voucherCode && !t.IsDeleted)
            .MaxAsync(t => (long?)t.DocumentNumber, ct) ?? 0;

        var disbTx = new LendingTransaction
        {
            VoucherType = voucherCode,
            DocumentNumber = maxDocNum + 1,
            PersonCode = application.PersonCode,
            CreditLineId = application.CreditLineId,
            PortfolioNumber = portfolioNumber,
            AccountCode = creditLine.AccountCode,
            TransactionDate = request.DisbursementDate,
            DebitAmount = approvedAmount,
            CreditAmount = 0,
            CostCenterId = application.CostCenterId,
            BranchId = application.BranchId,
            InterestRate = rate,
            TransactionCode = "DES", // Desembolso
            Description = $"Desembolso credito #{portfolioNumber} - Solicitud #{application.ApplicationNumber}",
            UserId = currentUser.UserName,
            SystemDate = dateTime.TodayUtc,
            Period = $"{request.DisbursementDate.Year}{request.DisbursementDate.Month:D2}",
            IdentificationNumber = person.TaxId,
            ApplicationId = (int)application.Id,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.LendingTransactions.Add(disbTx);

        // 9. Create Accounting Document (journal entry for disbursement)
        var accVoucherType = await context.VoucherTypes
            .FirstOrDefaultAsync(v => v.Code == "EG" && !v.IsDeleted, ct); // Egreso

        if (accVoucherType is not null)
        {
            var nextAccDocNum = accVoucherType.NextSequenceNumber + 1;
            accVoucherType.NextSequenceNumber = nextAccDocNum;

            var periodCode = request.DisbursementDate.Year * 100 + request.DisbursementDate.Month;

            var accDoc = new AccountingDocument
            {
                VoucherTypeCode = accVoucherType.Code,
                DocumentNumber = nextAccDocNum,
                Detail = $"Desembolso credito #{portfolioNumber} - {person.FirstName} {person.LastName}",
                TotalDebit = approvedAmount,
                TotalCredit = approvedAmount,
                DocumentDate = request.DisbursementDate,
                IsClosed = false,
                IsVoided = false,
                PeriodCode = periodCode,
                ModuleCode = "COP",
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.AccountingDocuments.Add(accDoc);

            // Debit: Portfolio account (Cartera)
            var portfolioAccount = await context.ChartOfAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountCode == creditLine.AccountCode && !a.IsDeleted, ct);

            // Credit: Bank/cash account (default first branch bank)
            var bankAccountCode = request.BankAccountCode ?? "11100501"; // Default cash account
            var bankAccount = await context.ChartOfAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountCode == bankAccountCode && !a.IsDeleted, ct);

            var defaultBranch = await context.Branches.AsNoTracking()
                .Where(b => !b.IsDeleted).OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
            var defaultCC = await context.CostCenters.AsNoTracking()
                .Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);

            var branchId = defaultBranch?.Id ?? 0;
            var ccId = defaultCC?.Id ?? 0;

            if (portfolioAccount is not null)
            {
                context.JournalEntries.Add(new JournalEntry
                {
                    VoucherTypeCode = accDoc.VoucherTypeCode,
                    DocumentNumber = accDoc.DocumentNumber,
                    AccountId = portfolioAccount.Id,
                    PersonId = person.Id,
                    BranchId = branchId,
                    CostCenterId = ccId,
                    PeriodCode = periodCode.ToString(),
                    TransactionDate = request.DisbursementDate,
                    Description = accDoc.Detail,
                    DebitAmount = approvedAmount,
                    CreditAmount = 0,
                    Status = 0,
                    UserName = currentUser.UserName,
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                });
            }

            if (bankAccount is not null)
            {
                context.JournalEntries.Add(new JournalEntry
                {
                    VoucherTypeCode = accDoc.VoucherTypeCode,
                    DocumentNumber = accDoc.DocumentNumber,
                    AccountId = bankAccount.Id,
                    PersonId = person.Id,
                    BranchId = branchId,
                    CostCenterId = ccId,
                    PeriodCode = periodCode.ToString(),
                    TransactionDate = request.DisbursementDate,
                    Description = accDoc.Detail,
                    DebitAmount = 0,
                    CreditAmount = approvedAmount,
                    Status = 0,
                    UserName = currentUser.UserName,
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                });
            }
        }

        // 10. Save all in transaction
        await context.SaveChangesAsync(ct);

        return Result.Success(new DisbursementResultDto(
            portfolio.PublicId,
            portfolioNumber,
            term,
            installmentAmount,
            totalInterest));
    }
}

public class DisburseLoanCommandValidator : AbstractValidator<DisburseLoanCommand>
{
    public DisburseLoanCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId)
            .NotEmpty().WithMessage("Id de solicitud requerido.");

        RuleFor(x => x.DisbursementDate)
            .NotEmpty().WithMessage("Fecha de desembolso requerida.");
    }
}
