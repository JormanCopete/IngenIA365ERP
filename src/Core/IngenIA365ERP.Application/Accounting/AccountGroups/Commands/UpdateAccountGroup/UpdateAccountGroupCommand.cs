using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountGroups.Commands.UpdateAccountGroup;

public record UpdateAccountGroupCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int? GroupNumber { get; init; }
    public int? GroupType { get; init; }
    public string? AccountCode { get; init; }
    public string? Description { get; init; }
    public int? ReportOrder { get; init; }
    public int? Level { get; init; }
    public int? ParentGroupId { get; init; }
}

public class UpdateAccountGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAccountGroupCommand, Result>
{
    public async Task<Result> Handle(
        UpdateAccountGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.AccountGroups
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.GroupNumber = request.GroupNumber;
        entity.GroupType = request.GroupType;
        entity.AccountCode = request.AccountCode;
        entity.Description = request.Description;
        entity.ReportOrder = request.ReportOrder;
        entity.Level = request.Level;
        entity.ParentGroupId = request.ParentGroupId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
