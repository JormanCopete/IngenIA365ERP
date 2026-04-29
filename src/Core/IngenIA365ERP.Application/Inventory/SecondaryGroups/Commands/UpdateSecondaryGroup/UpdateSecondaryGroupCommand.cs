using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.SecondaryGroups.Commands.UpdateSecondaryGroup;

public record UpdateSecondaryGroupCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? PrimaryGroupId { get; init; }
}

public class UpdateSecondaryGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSecondaryGroupCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSecondaryGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SecondaryGroups
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.GroupCode = request.GroupCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.PrimaryGroupId = request.PrimaryGroupId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
