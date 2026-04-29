using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ScoringParameters.Commands.UpdateScoringParameter;

public record UpdateScoringParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string CriterionCode { get; init; } = string.Empty;
    public string SubItemCode { get; init; } = string.Empty;
    public string CriterionName { get; init; } = string.Empty;
    public string SubItemName { get; init; } = string.Empty;
    public decimal Percentage { get; init; }
}

public class UpdateScoringParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateScoringParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateScoringParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ScoringParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.CriterionCode = request.CriterionCode;
        entity.SubItemCode = request.SubItemCode;
        entity.CriterionName = request.CriterionName;
        entity.SubItemName = request.SubItemName;
        entity.Percentage = request.Percentage;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
