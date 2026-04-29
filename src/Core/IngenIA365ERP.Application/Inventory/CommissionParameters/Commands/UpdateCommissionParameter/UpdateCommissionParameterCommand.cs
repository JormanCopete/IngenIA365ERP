using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.CommissionParameters.Commands.UpdateCommissionParameter;

public record UpdateCommissionParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int InvoiceTypeId { get; init; }
    public int CommissionGroupId { get; init; }
    public int GroupId { get; init; }
    public decimal SalesRangeStart { get; init; }
    public decimal SalesRangeEnd { get; init; }
    public decimal CommissionRate { get; init; }
}

public class UpdateCommissionParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCommissionParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCommissionParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CommissionParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.InvoiceTypeId = request.InvoiceTypeId;
        entity.CommissionGroupId = request.CommissionGroupId;
        entity.GroupId = request.GroupId;
        entity.SalesRangeStart = request.SalesRangeStart;
        entity.SalesRangeEnd = request.SalesRangeEnd;
        entity.CommissionRate = request.CommissionRate;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
