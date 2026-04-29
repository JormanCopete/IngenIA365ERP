using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Zones.Commands.UpdateZone;

public record UpdateZoneCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ZoneId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public int SubZoneId { get; init; }
}

public class UpdateZoneCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateZoneCommand, Result>
{
    public async Task<Result> Handle(
        UpdateZoneCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Zones
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ZoneId = request.ZoneId;
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.Address = request.Address ?? string.Empty;
        entity.Phone = request.Phone ?? string.Empty;
        entity.SubZoneId = request.SubZoneId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
