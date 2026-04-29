using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Locations.Commands.UpdateLocation;

public record UpdateLocationCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int LocationCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

public class UpdateLocationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateLocationCommand, Result>
{
    public async Task<Result> Handle(
        UpdateLocationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Locations
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.LocationCode = request.LocationCode;
        entity.Description = request.Description;
        entity.ShortDescription = request.ShortDescription;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
