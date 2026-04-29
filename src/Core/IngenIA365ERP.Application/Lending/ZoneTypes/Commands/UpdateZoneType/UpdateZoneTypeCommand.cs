using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ZoneTypes.Commands.UpdateZoneType;

public record UpdateZoneTypeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ZoneTypeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class UpdateZoneTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateZoneTypeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateZoneTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ZoneTypes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ZoneTypeId = request.ZoneTypeId;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
