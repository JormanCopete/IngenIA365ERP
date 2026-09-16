using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments.Commands.ProcessPayment;

public record ProcessPaymentCommand : IRequest<Result<PaymentResultDto>>, IReintentableAnteConcurrencia
{
    public Guid PortfolioPublicId { get; init; }
    public decimal Amount { get; init; }
    public DateOnly PaymentDate { get; init; }
    public string PaymentMethod { get; init; } = "EF"; // EF=Efectivo, CH=Cheque, TR=Transferencia
    public string? Reference { get; init; }
    /// <summary>Banco que recibe el recaudo: su cuenta contable va al débito (feature 009).</summary>
    public Guid? BankPublicId { get; init; }
    /// <summary>O, en su defecto, el código de la cuenta de caja.</summary>
    public string? CashAccountCode { get; init; }
}

public record PaymentResultDto(
    Guid TransactionPublicId,
    long DocumentNumber,
    decimal PaidCapital,
    decimal PaidInterest,
    decimal PaidDefault,
    decimal Remaining,
    int InstallmentsCovered,
    bool IsFullyPaid);

public record PaymentDistributionLine(
    int Period,
    decimal Capital,
    decimal Interest,
    decimal Default);

public class ProcessPaymentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser,
    AccountingPoster poster,
    AccountEligibility cuentas)
    : IRequestHandler<ProcessPaymentCommand, Result<PaymentResultDto>>
{
    public async Task<Result<PaymentResultDto>> Handle(ProcessPaymentCommand request, CancellationToken ct)
    {
        // 1. Find portfolio
        var portfolio = await context.LoanPortfolios
            .Include(lp => lp.CreditLine)
            .Include(lp => lp.Person)
            .FirstOrDefaultAsync(lp => lp.PublicId == request.PortfolioPublicId && !lp.IsDeleted, ct);

        if (portfolio is null)
            return Result.Failure<PaymentResultDto>(new Error("Payment.PortfolioNotFound",
                "Credito no encontrado."));

        if (portfolio.CurrentBalance <= 0)
            return Result.Failure<PaymentResultDto>(new Error("Payment.AlreadyPaid",
                "El credito ya fue cancelado."));

        // 2. Load pending installments ordered by ProcessDate (oldest first)
        var personCode = portfolio.Person?.LegacyCode ?? portfolio.Person?.TaxId ?? portfolio.IdentificationNumber;
        var installments = await context.PendingInstallments
            .Where(pi => pi.PortfolioNumber == portfolio.PortfolioNumber
                      && pi.CreditLineId == portfolio.CreditLineId
                      && pi.PersonCode == personCode
                      && !pi.IsDeleted
                      && (pi.BalanceCapital > 0 || pi.BalanceInterest > 0 || pi.DefaultInterestBalance > 0))
            .OrderBy(pi => pi.AccrualPeriod)
            .ToListAsync(ct);

        if (installments.Count == 0)
            return Result.Failure<PaymentResultDto>(new Error("Payment.NoInstallments",
                "No hay cuotas pendientes de pago."));

        // 3. Apply payment in priority order per installment:
        //    a. Default interest (mora)
        //    b. Current interest
        //    c. Capital
        var remaining = request.Amount;
        decimal totalPaidCapital = 0, totalPaidInterest = 0, totalPaidDefault = 0;
        int installmentsCovered = 0;

        foreach (var inst in installments)
        {
            if (remaining <= 0) break;

            // a. Default interest first
            if (inst.DefaultInterestBalance > 0 && remaining > 0)
            {
                var payDefault = Math.Min(remaining, inst.DefaultInterestBalance);
                inst.DefaultInterestPaid += payDefault;
                inst.DefaultInterestBalance -= payDefault;
                remaining -= payDefault;
                totalPaidDefault += payDefault;
            }

            // b. Interest
            if (inst.BalanceInterest > 0 && remaining > 0)
            {
                var payInterest = Math.Min(remaining, inst.BalanceInterest);
                inst.PaidInterest += payInterest;
                inst.BalanceInterest -= payInterest;
                remaining -= payInterest;
                totalPaidInterest += payInterest;
            }

            // c. Capital
            if (inst.BalanceCapital > 0 && remaining > 0)
            {
                var payCapital = Math.Min(remaining, inst.BalanceCapital);
                inst.PaidCapital += payCapital;
                inst.BalanceCapital -= payCapital;
                remaining -= payCapital;
                totalPaidCapital += payCapital;
            }

            // Update total balance
            inst.TotalBalance = inst.BalanceCapital + inst.BalanceInterest + inst.DefaultInterestBalance;
            inst.UpdatedAt = dateTime.UtcNow;
            inst.UpdatedBy = currentUser.UserName;

            // Check if fully paid
            if (inst.BalanceCapital == 0 && inst.BalanceInterest == 0)
                installmentsCovered++;
        }

        // 4. Update portfolio
        portfolio.CurrentBalance -= totalPaidCapital;
        if (portfolio.CurrentBalance < 0) portfolio.CurrentBalance = 0;
        portfolio.PaidInstallments += installmentsCovered;
        portfolio.PendingInstallmentCount -= installmentsCovered;
        portfolio.LastPaymentDate = request.PaymentDate;

        // Update portfolio balance fields
        portfolio.CapitalBalancePayment = totalPaidCapital;
        portfolio.InterestBalancePayment = totalPaidInterest;
        portfolio.DefaultBalancePayment = totalPaidDefault;
        portfolio.UpdatedAt = dateTime.UtcNow;
        portfolio.UpdatedBy = currentUser.UserName;

        var isFullyPaid = portfolio.CurrentBalance == 0;
        if (isFullyPaid)
        {
            portfolio.ClosingDate = request.PaymentDate;
        }

        // 5. Create LendingTransaction
        var voucherCode = "RC"; // Recaudo
        var maxDocNum = await context.LendingTransactions
            .Where(t => t.VoucherType == voucherCode && !t.IsDeleted)
            .MaxAsync(t => (long?)t.DocumentNumber, ct) ?? 0;

        var totalPayment = totalPaidCapital + totalPaidInterest + totalPaidDefault;
        var transaction = new LendingTransaction
        {
            VoucherType = voucherCode,
            DocumentNumber = maxDocNum + 1,
            PersonCode = personCode,
            CreditLineId = portfolio.CreditLineId,
            PortfolioNumber = portfolio.PortfolioNumber,
            AccountCode = portfolio.CreditLine?.AccountCode ?? "",
            TransactionDate = request.PaymentDate,
            DebitAmount = 0,
            CreditAmount = totalPayment,
            CostCenterId = portfolio.CostCenterId,
            BranchId = portfolio.BranchId,
            InterestRate = portfolio.InterestRate,
            TransactionCode = "REC",
            Description = $"Recaudo credito #{portfolio.PortfolioNumber} - Cap:{totalPaidCapital:N0} Int:{totalPaidInterest:N0} Mora:{totalPaidDefault:N0}",
            UserId = currentUser.UserName,
            SystemDate = dateTime.TodayUtc,
            Period = $"{request.PaymentDate.Year}{request.PaymentDate.Month:D2}",
            IdentificationNumber = portfolio.IdentificationNumber,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.LendingTransactions.Add(transaction);

        // 6. Comprobante RC por el contrato de contabilización (feature 009, FR-036): caja o banco al
        //    débito; capital, intereses y mora al crédito con las cuentas de la línea de crédito, el
        //    asociado como tercero y el pagaré como documento cruce. Sin parametrización no hay recaudo.
        var codigoCaja = request.BankPublicId is { } bancoId
            ? await context.Banks.AsNoTracking().Where(b => b.PublicId == bancoId && !b.IsDeleted).Select(b => b.AccountingAccountCode).FirstOrDefaultAsync(ct)
            : request.CashAccountCode;
        if (string.IsNullOrWhiteSpace(codigoCaja))
            return Result.Failure<PaymentResultDto>(AccountingErrors.ParameterizationMissing(ModuloContable.Cartera, "la cuenta contable de caja o banco del recaudo"));
        var cuentaCaja = await cuentas.ResolverPorCodigoAsync(codigoCaja, ModuloContable.Cartera, ct);
        if (cuentaCaja.IsFailure) return Result.Failure<PaymentResultDto>(cuentaCaja.Error);
        var linea = portfolio.CreditLine;
        if (linea is null)
            return Result.Failure<PaymentResultDto>(AccountingErrors.ParameterizationMissing(ModuloContable.Cartera, "la línea de crédito del crédito"));

        var personName = portfolio.Person is not null ? $"{portfolio.Person.FirstName} {portfolio.Person.LastName}" : portfolio.IdentificationNumber;
        var detalle = $"Recaudo crédito #{portfolio.PortfolioNumber} - {personName}";
        var pagare = portfolio.PortfolioNumber.ToString();
        var lineas = new List<PostingLine>
        {
            new() { AccountId = cuentaCaja.Value.Id, Debit = totalPayment, Detail = detalle, PersonId = portfolio.PersonId },
        };
        foreach (var (valor, codigo, que, concepto) in new[]
                 {
                     (totalPaidCapital, linea.AccountCode, "la cuenta de cartera de la línea", "Abono capital"),
                     (totalPaidInterest, linea.AccountInterestIncome, "la cuenta de ingreso por intereses de la línea", "Ingreso intereses"),
                     (totalPaidDefault, linea.AccountInterestDefault, "la cuenta de intereses de mora de la línea", "Ingreso intereses de mora"),
                 })
        {
            if (valor <= 0m) continue;
            if (string.IsNullOrWhiteSpace(codigo))
                return Result.Failure<PaymentResultDto>(AccountingErrors.ParameterizationMissing(ModuloContable.Cartera, $"{que} {linea.Description}"));
            var cuenta = await cuentas.ResolverPorCodigoAsync(codigo, ModuloContable.Cartera, ct);
            if (cuenta.IsFailure) return Result.Failure<PaymentResultDto>(cuenta.Error);
            lineas.Add(new PostingLine { AccountId = cuenta.Value.Id, Credit = valor, Detail = $"{concepto} crédito #{portfolio.PortfolioNumber}", PersonId = portfolio.PersonId, CrossDocumentType = "PG", CrossDocumentNumber = pagare });
        }
        var posting = await poster.PrepareAsync(new PostingRequest("RC", request.PaymentDate, detalle,
            new AccountingOrigin(ModuloContable.Cartera, "LoanPayment", transaction.PublicId), lineas), ct);
        if (posting.IsFailure) return Result.Failure<PaymentResultDto>(posting.Error);

        // 7. Save all
        await context.SaveChangesAsync(ct);

        return Result.Success(new PaymentResultDto(
            transaction.PublicId,
            transaction.DocumentNumber,
            totalPaidCapital,
            totalPaidInterest,
            totalPaidDefault,
            remaining,
            installmentsCovered,
            isFullyPaid));
    }
}

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.PortfolioPublicId)
            .NotEmpty().WithMessage("Credito requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El valor del pago debe ser mayor a cero.");

        RuleFor(x => x.PaymentDate)
            .NotEmpty().WithMessage("Fecha de pago requerida.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Forma de pago requerida.");
    }
}
