using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.CostCenters.Commands.UpdateCostCenter;

public record UpdateCostCenterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? CompanyTaxId { get; init; }
    public short PayrollType { get; init; }
    public short Period { get; init; }
    public short PayrollPeriodicity { get; init; }
    public string? PayrollStatus { get; init; }
}

public class UpdateCostCenterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCostCenterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCostCenterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CostCenters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Name = request.Name;
        entity.CompanyName = request.CompanyName;
        entity.CompanyTaxId = request.CompanyTaxId;
        entity.PayrollType = request.PayrollType;
        entity.Period = request.Period;
        entity.PayrollPeriodicity = request.PayrollPeriodicity;
        entity.PayrollStatus = request.PayrollStatus;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
