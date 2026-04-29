using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.DebitDailyParameters.Commands.UpdateDebitDailyParameter;

public record UpdateDebitDailyParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ParameterCode { get; init; }
    public string BankId { get; init; } = string.Empty;
    public string BatchVoucherCode { get; init; } = string.Empty;
    public string OnlineVoucherCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int NewCardsCount { get; init; }
    public string? LastCardNumber { get; init; }
    public string? ClosingVoucherCode { get; init; }
    public long ClosingSequenceNumber { get; init; }
    public string? PosClosingVoucherCode { get; init; }
    public int PosClosingSequence { get; init; }
    public string? ClosingFlag { get; init; }
    public decimal NetworkCommission { get; init; }
    public decimal OtherNetworkCommission { get; init; }
}

public class UpdateDebitDailyParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDebitDailyParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateDebitDailyParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DebitDailyParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ParameterCode = request.ParameterCode;
        entity.BankId = request.BankId;
        entity.BatchVoucherCode = request.BatchVoucherCode;
        entity.OnlineVoucherCode = request.OnlineVoucherCode;
        entity.Description = request.Description;
        entity.NewCardsCount = request.NewCardsCount;
        entity.LastCardNumber = request.LastCardNumber;
        entity.ClosingVoucherCode = request.ClosingVoucherCode;
        entity.ClosingSequenceNumber = request.ClosingSequenceNumber;
        entity.PosClosingVoucherCode = request.PosClosingVoucherCode;
        entity.PosClosingSequence = request.PosClosingSequence;
        entity.ClosingFlag = request.ClosingFlag;
        entity.NetworkCommission = request.NetworkCommission;
        entity.OtherNetworkCommission = request.OtherNetworkCommission;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
