using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Banks.Commands.UpdateBank;

public record UpdateBankCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
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

public class UpdateBankCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateBankCommand, Result>
{
    public async Task<Result> Handle(
        UpdateBankCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Banks
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.AccountCode = request.AccountCode;
        entity.VoucherTypeCode = request.VoucherTypeCode;
        entity.TransferCode = request.TransferCode;
        entity.AccountClass = request.AccountClass;
        entity.CheckDigitRequired = request.CheckDigitRequired;
        entity.LastCheckNumber = request.LastCheckNumber;
        entity.AccountingAccountCode = request.AccountingAccountCode;
        entity.PrintFormat = request.PrintFormat;
        entity.Copies = request.Copies;
        entity.FinancialTaxRate = request.FinancialTaxRate;
        entity.FileStructure = request.FileStructure;
        entity.ChargesCommission = request.ChargesCommission;
        entity.CommissionAccount = request.CommissionAccount;
        entity.CommissionType = request.CommissionType;
        entity.CommissionAmount = request.CommissionAmount;
        entity.PromptForPrinter = request.PromptForPrinter;
        entity.ControlSequential = request.ControlSequential;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
