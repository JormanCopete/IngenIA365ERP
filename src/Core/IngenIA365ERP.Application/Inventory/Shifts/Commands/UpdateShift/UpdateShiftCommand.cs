using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Shifts.Commands.UpdateShift;

public record UpdateShiftCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ShiftCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
}

public class UpdateShiftCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateShiftCommand, Result>
{
    public async Task<Result> Handle(
        UpdateShiftCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Shifts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ShiftCode = request.ShiftCode;
        entity.Name = request.Name;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
