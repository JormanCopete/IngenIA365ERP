using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountSubgroups.Commands.UpdateAccountSubgroup;

public record UpdateAccountSubgroupCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int? GroupId { get; init; }
    public int SubgroupNumber { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? ReportOrder { get; init; }
    public int? Level { get; init; }
}

public class UpdateAccountSubgroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAccountSubgroupCommand, Result>
{
    public async Task<Result> Handle(
        UpdateAccountSubgroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.AccountSubgroups
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.GroupId = request.GroupId;
        entity.SubgroupNumber = request.SubgroupNumber;
        entity.AccountCode = request.AccountCode;
        entity.Description = request.Description;
        entity.ReportOrder = request.ReportOrder;
        entity.Level = request.Level;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
