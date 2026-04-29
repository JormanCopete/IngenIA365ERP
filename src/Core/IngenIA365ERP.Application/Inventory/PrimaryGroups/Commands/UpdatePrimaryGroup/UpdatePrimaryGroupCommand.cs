using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.PrimaryGroups.Commands.UpdatePrimaryGroup;

public record UpdatePrimaryGroupCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class UpdatePrimaryGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePrimaryGroupCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePrimaryGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PrimaryGroups
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.GroupCode = request.GroupCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
