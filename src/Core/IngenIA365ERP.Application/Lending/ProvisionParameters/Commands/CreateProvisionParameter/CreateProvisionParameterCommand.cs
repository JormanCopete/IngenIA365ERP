using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.ProvisionParameters.Commands.CreateProvisionParameter;

public record CreateProvisionParameterCommand : IRequest<Result<Guid>>
{
    public int Period { get; init; }
    public int Code { get; init; }
    public decimal RateB { get; init; }
    public decimal RateC { get; init; }
    public decimal RateD { get; init; }
    public decimal RateE { get; init; }
}

public class CreateProvisionParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateProvisionParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProvisionParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ProvisionParameter
        {
            Period = request.Period,
            Code = request.Code,
            RateB = request.RateB,
            RateC = request.RateC,
            RateD = request.RateD,
            RateE = request.RateE,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.ProvisionParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateProvisionParameterCommandValidator : AbstractValidator<CreateProvisionParameterCommand>
{
    public CreateProvisionParameterCommandValidator()
    {
        RuleFor(x => x.Period)
            .GreaterThan(0).WithMessage("Period must be greater than 0.");

        RuleFor(x => x.Code)
            .GreaterThan(0).WithMessage("Code must be greater than 0.");

        RuleFor(x => x.RateB)
            .GreaterThanOrEqualTo(0).WithMessage("Rate B must be non-negative.");

        RuleFor(x => x.RateC)
            .GreaterThanOrEqualTo(0).WithMessage("Rate C must be non-negative.");

        RuleFor(x => x.RateD)
            .GreaterThanOrEqualTo(0).WithMessage("Rate D must be non-negative.");

        RuleFor(x => x.RateE)
            .GreaterThanOrEqualTo(0).WithMessage("Rate E must be non-negative.");
    }
}
