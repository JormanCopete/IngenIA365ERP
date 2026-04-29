using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.HousingParameters.Commands.UpdateHousingParameter;

public record UpdateHousingParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string PersonCode { get; init; } = string.Empty;
    public int CreditLineId { get; init; }
    public long PortfolioNumber { get; init; }
    public int HousingClass { get; init; }
    public int HousingType { get; init; }
    public string SocialInterest { get; init; } = string.Empty;
    public string HasSubsidy { get; init; } = string.Empty;
    public int NetworkEntity { get; init; }
    public long NetworkValue { get; init; }
    public int DisbursementType { get; init; }
    public int CurrencyType { get; init; }
}

public class UpdateHousingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateHousingParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateHousingParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.HousingParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.PersonCode = request.PersonCode;
        entity.CreditLineId = request.CreditLineId;
        entity.PortfolioNumber = request.PortfolioNumber;
        entity.HousingClass = request.HousingClass;
        entity.HousingType = request.HousingType;
        entity.SocialInterest = request.SocialInterest;
        entity.HasSubsidy = request.HasSubsidy;
        entity.NetworkEntity = request.NetworkEntity;
        entity.NetworkValue = request.NetworkValue;
        entity.DisbursementType = request.DisbursementType;
        entity.CurrencyType = request.CurrencyType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
