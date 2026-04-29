using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Commands.UpdatePayPeriod;

public record UpdatePayPeriodCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int PlanId { get; init; }
    public int PayrollCompanyId { get; init; }
    public string? Description { get; init; }
    public string? PayDate { get; init; }
    public string? LiquidationCompanyId { get; init; }
    public int? CycleMonth { get; init; }
    public int? CycleHours { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? Periodicity { get; init; }
    public int Status { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public int PeriodId { get; init; }
}

public class UpdatePayPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePayPeriodCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePayPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayPeriods
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.PlanId = request.PlanId;
        entity.PayrollCompanyId = request.PayrollCompanyId;
        entity.Description = request.Description;
        entity.PayDate = request.PayDate;
        entity.LiquidationCompanyId = request.LiquidationCompanyId;
        entity.CycleMonth = request.CycleMonth;
        entity.CycleHours = request.CycleHours;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Periodicity = request.Periodicity;
        entity.Status = request.Status;
        entity.StatusMessage = request.StatusMessage;
        entity.PeriodId = request.PeriodId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
