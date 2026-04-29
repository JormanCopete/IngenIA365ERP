using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.HousingParameters.Commands.CreateHousingParameter;

public record CreateHousingParameterCommand : IRequest<Result<Guid>>
{
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

public class CreateHousingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateHousingParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateHousingParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new HousingParameter
        {
            PersonCode = request.PersonCode,
            CreditLineId = request.CreditLineId,
            PortfolioNumber = request.PortfolioNumber,
            HousingClass = request.HousingClass,
            HousingType = request.HousingType,
            SocialInterest = request.SocialInterest,
            HasSubsidy = request.HasSubsidy,
            NetworkEntity = request.NetworkEntity,
            NetworkValue = request.NetworkValue,
            DisbursementType = request.DisbursementType,
            CurrencyType = request.CurrencyType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.HousingParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateHousingParameterCommandValidator : AbstractValidator<CreateHousingParameterCommand>
{
    public CreateHousingParameterCommandValidator()
    {
        RuleFor(x => x.PersonCode)
            .NotEmpty().WithMessage("PersonCode is required.")
            .MaximumLength(20).WithMessage("PersonCode must not exceed 20 characters.");

        RuleFor(x => x.SocialInterest)
            .MaximumLength(2).WithMessage("SocialInterest must not exceed 2 characters.");

        RuleFor(x => x.HasSubsidy)
            .MaximumLength(2).WithMessage("HasSubsidy must not exceed 2 characters.");
    }
}
