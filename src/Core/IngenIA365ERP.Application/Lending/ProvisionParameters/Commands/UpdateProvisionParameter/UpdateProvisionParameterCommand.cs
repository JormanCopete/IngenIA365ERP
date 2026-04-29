using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ProvisionParameters.Commands.UpdateProvisionParameter;

public record UpdateProvisionParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int Period { get; init; }
    public int Code { get; init; }
    public decimal RateB { get; init; }
    public decimal RateC { get; init; }
    public decimal RateD { get; init; }
    public decimal RateE { get; init; }
}

public class UpdateProvisionParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateProvisionParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateProvisionParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ProvisionParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Period = request.Period;
        entity.Code = request.Code;
        entity.RateB = request.RateB;
        entity.RateC = request.RateC;
        entity.RateD = request.RateD;
        entity.RateE = request.RateE;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
