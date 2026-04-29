using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsParameters.Commands.UpdateSavingsParameter;

public record UpdateSavingsParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int SavingsLineId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string InterestPaymentPeriod { get; init; } = string.Empty;
    public decimal MinInterestBalance { get; init; }
    public decimal MinTransactionAmount { get; init; }
    public decimal MinAccountBalance { get; init; }
    public decimal InterestPaymentRate { get; init; }
    public decimal MinWithholdingAmount { get; init; }
    public decimal WithholdingRate { get; init; }
    public int ClearingDays { get; init; }
    public decimal MaxWithdrawalAmount { get; init; }
    public decimal TaxRate { get; init; }
}

public class UpdateSavingsParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSavingsParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSavingsParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SavingsParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.SavingsLineId = request.SavingsLineId;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.InterestPaymentPeriod = request.InterestPaymentPeriod;
        entity.MinInterestBalance = request.MinInterestBalance;
        entity.MinTransactionAmount = request.MinTransactionAmount;
        entity.MinAccountBalance = request.MinAccountBalance;
        entity.InterestPaymentRate = request.InterestPaymentRate;
        entity.MinWithholdingAmount = request.MinWithholdingAmount;
        entity.WithholdingRate = request.WithholdingRate;
        entity.ClearingDays = request.ClearingDays;
        entity.MaxWithdrawalAmount = request.MaxWithdrawalAmount;
        entity.TaxRate = request.TaxRate;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
