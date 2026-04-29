using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountingPeriods.Commands.UpdateAccountingPeriod;

public record UpdateAccountingPeriodCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string ModuleCode { get; init; } = string.Empty;
    public int Year { get; init; }
    public byte PeriodNumber { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = "O";
}

public class UpdateAccountingPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAccountingPeriodCommand, Result>
{
    public async Task<Result> Handle(
        UpdateAccountingPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.AccountingPeriods
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ModuleCode = request.ModuleCode;
        entity.Year = request.Year;
        entity.PeriodNumber = request.PeriodNumber;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Status = request.Status;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
