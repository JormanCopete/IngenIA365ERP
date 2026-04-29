using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.UpdateWorkRiskRate;

public record UpdateWorkRiskRateCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public decimal Rate { get; init; }
}

public class UpdateWorkRiskRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWorkRiskRateCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWorkRiskRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WorkRiskRates
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.Rate = request.Rate;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
