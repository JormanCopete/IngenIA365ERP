using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.WithdrawalReasons.Commands.UpdateWithdrawalReason;

public record UpdateWithdrawalReasonCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class UpdateWithdrawalReasonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWithdrawalReasonCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWithdrawalReasonCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithdrawalReasons
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
