using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WithdrawalStatuses.Commands.UpdateWithdrawalStatus;

public record UpdateWithdrawalStatusCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string PersonCode { get; init; } = string.Empty;
    public DateOnly RequestDate { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? EffectiveDate { get; init; }
    public string? Remarks { get; init; }
    public int Period { get; init; }
}

public class UpdateWithdrawalStatusCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWithdrawalStatusCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWithdrawalStatusCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithdrawalStatuses
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.PersonCode = request.PersonCode;
        entity.RequestDate = request.RequestDate;
        entity.ReasonCode = request.ReasonCode;
        entity.Status = request.Status;
        entity.EffectiveDate = request.EffectiveDate;
        entity.Remarks = request.Remarks;
        entity.Period = request.Period;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
