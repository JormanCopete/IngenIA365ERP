using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.ProductGroups.Commands.UpdateProductGroup;

public record UpdateProductGroupCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? SecondaryGroupId { get; init; }
    public bool RestrictsLimit { get; init; }
    public int MaxSalesQuantity { get; init; }
}

public class UpdateProductGroupCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateProductGroupCommand, Result>
{
    public async Task<Result> Handle(
        UpdateProductGroupCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ProductGroups
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.GroupCode = request.GroupCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.SecondaryGroupId = request.SecondaryGroupId;
        entity.RestrictsLimit = request.RestrictsLimit;
        entity.MaxSalesQuantity = request.MaxSalesQuantity;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
