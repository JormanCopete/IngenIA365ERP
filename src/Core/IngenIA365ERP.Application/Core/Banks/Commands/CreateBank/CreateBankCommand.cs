using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.Banks.Commands.CreateBank;

public record CreateBankCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? AccountCode { get; init; }
    public string? VoucherTypeCode { get; init; }
    public string? TransferCode { get; init; }
    public string? AccountClass { get; init; }
    public bool CheckDigitRequired { get; init; }
    public int? LastCheckNumber { get; init; }
    public string? AccountingAccountCode { get; init; }
    public string? PrintFormat { get; init; }
    public short? Copies { get; init; }
    public decimal FinancialTaxRate { get; init; }
    public string? FileStructure { get; init; }
    public bool ChargesCommission { get; init; }
    public string? CommissionAccount { get; init; }
    public int? CommissionType { get; init; }
    public decimal? CommissionAmount { get; init; }
    public bool PromptForPrinter { get; init; }
    public string? ControlSequential { get; init; }
}

public class CreateBankCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateBankCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateBankCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Bank
        {
            Name = request.Name,
            ShortName = request.ShortName,
            AccountCode = request.AccountCode,
            VoucherTypeCode = request.VoucherTypeCode,
            TransferCode = request.TransferCode,
            AccountClass = request.AccountClass,
            CheckDigitRequired = request.CheckDigitRequired,
            LastCheckNumber = request.LastCheckNumber,
            AccountingAccountCode = request.AccountingAccountCode,
            PrintFormat = request.PrintFormat,
            Copies = request.Copies,
            FinancialTaxRate = request.FinancialTaxRate,
            FileStructure = request.FileStructure,
            ChargesCommission = request.ChargesCommission,
            CommissionAccount = request.CommissionAccount,
            CommissionType = request.CommissionType,
            CommissionAmount = request.CommissionAmount,
            PromptForPrinter = request.PromptForPrinter,
            ControlSequential = request.ControlSequential,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Banks.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateBankCommandValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(30).WithMessage("Short name must not exceed 30 characters.");

        RuleFor(x => x.AccountCode)
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.VoucherTypeCode)
            .MaximumLength(10).WithMessage("Voucher type code must not exceed 10 characters.");

        RuleFor(x => x.TransferCode)
            .MaximumLength(20).WithMessage("Transfer code must not exceed 20 characters.");

        RuleFor(x => x.AccountClass)
            .MaximumLength(2).WithMessage("Account class must not exceed 2 characters.");

        RuleFor(x => x.AccountingAccountCode)
            .MaximumLength(20).WithMessage("Accounting account code must not exceed 20 characters.");

        RuleFor(x => x.PrintFormat)
            .MaximumLength(2).WithMessage("Print format must not exceed 2 characters.");

        RuleFor(x => x.FileStructure)
            .MaximumLength(4).WithMessage("File structure must not exceed 4 characters.");

        RuleFor(x => x.CommissionAccount)
            .MaximumLength(20).WithMessage("Commission account must not exceed 20 characters.");

        RuleFor(x => x.ControlSequential)
            .MaximumLength(2).WithMessage("Control sequential must not exceed 2 characters.");

        RuleFor(x => x.FinancialTaxRate)
            .GreaterThanOrEqualTo(0).WithMessage("Financial tax rate must be non-negative.");

        RuleFor(x => x.CommissionAmount)
            .GreaterThanOrEqualTo(0).When(x => x.CommissionAmount.HasValue)
            .WithMessage("Commission amount must be non-negative.");
    }
}
