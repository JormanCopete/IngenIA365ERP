using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.PeriodicityParameters.Commands.CreatePeriodicityParameter;

public record CreatePeriodicityParameterCommand : IRequest<Result<Guid>>
{
    public string CompanyCode { get; init; } = string.Empty;
    public string DeductionClass { get; init; } = string.Empty;
    public string Periodicity { get; init; } = string.Empty;
    public int StartDay { get; init; }
    public int EndDay { get; init; }
    public string DayCount { get; init; } = string.Empty;
}

public class CreatePeriodicityParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePeriodicityParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePeriodicityParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PeriodicityParameter
        {
            CompanyCode = request.CompanyCode,
            DeductionClass = request.DeductionClass,
            Periodicity = request.Periodicity,
            StartDay = request.StartDay,
            EndDay = request.EndDay,
            DayCount = request.DayCount,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PeriodicityParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePeriodicityParameterCommandValidator : AbstractValidator<CreatePeriodicityParameterCommand>
{
    public CreatePeriodicityParameterCommandValidator()
    {
        RuleFor(x => x.CompanyCode)
            .MaximumLength(5).WithMessage("CompanyCode must not exceed 5 characters.");

        RuleFor(x => x.DeductionClass)
            .MaximumLength(3).WithMessage("DeductionClass must not exceed 3 characters.");

        RuleFor(x => x.Periodicity)
            .MaximumLength(2).WithMessage("Periodicity must not exceed 2 characters.");

        RuleFor(x => x.DayCount)
            .MaximumLength(3).WithMessage("DayCount must not exceed 3 characters.");
    }
}
