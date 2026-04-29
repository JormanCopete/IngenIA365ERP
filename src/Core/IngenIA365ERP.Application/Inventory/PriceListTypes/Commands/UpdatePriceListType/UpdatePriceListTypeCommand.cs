using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.PriceListTypes.Commands.UpdatePriceListType;

public record UpdatePriceListTypeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? PriceClass { get; init; }
}

public class UpdatePriceListTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePriceListTypeCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePriceListTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PriceListTypes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.TypeCode = request.TypeCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.PriceClass = request.PriceClass;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
