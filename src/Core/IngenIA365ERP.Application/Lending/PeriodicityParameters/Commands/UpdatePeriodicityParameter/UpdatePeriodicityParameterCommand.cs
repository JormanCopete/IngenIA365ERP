using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PeriodicityParameters.Commands.UpdatePeriodicityParameter;

public record UpdatePeriodicityParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string CompanyCode { get; init; } = string.Empty;
    public string DeductionClass { get; init; } = string.Empty;
    public string Periodicity { get; init; } = string.Empty;
    public int StartDay { get; init; }
    public int EndDay { get; init; }
    public string DayCount { get; init; } = string.Empty;
}

public class UpdatePeriodicityParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePeriodicityParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePeriodicityParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PeriodicityParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.CompanyCode = request.CompanyCode;
        entity.DeductionClass = request.DeductionClass;
        entity.Periodicity = request.Periodicity;
        entity.StartDay = request.StartDay;
        entity.EndDay = request.EndDay;
        entity.DayCount = request.DayCount;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
