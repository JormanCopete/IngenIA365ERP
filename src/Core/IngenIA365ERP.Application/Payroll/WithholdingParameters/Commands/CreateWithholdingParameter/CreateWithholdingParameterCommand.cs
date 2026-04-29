using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.CreateWithholdingParameter;

public record CreateWithholdingParameterCommand : IRequest<Result<Guid>>
{
    public int PayrollCompanyId { get; init; }
    public int UvtRangeStart { get; init; }
    public int UvtRangeEnd { get; init; }
    public decimal Rate { get; init; }
    public int AdditionalUvt { get; init; }
}

public class CreateWithholdingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWithholdingParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateWithholdingParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new WithholdingParameter
        {
            PayrollCompanyId = request.PayrollCompanyId,
            UvtRangeStart = request.UvtRangeStart,
            UvtRangeEnd = request.UvtRangeEnd,
            Rate = request.Rate,
            AdditionalUvt = request.AdditionalUvt,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.WithholdingParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWithholdingParameterCommandValidator : AbstractValidator<CreateWithholdingParameterCommand>
{
    public CreateWithholdingParameterCommandValidator()
    {
        RuleFor(x => x.UvtRangeEnd)
            .GreaterThanOrEqualTo(x => x.UvtRangeStart)
            .WithMessage("UVT range end must be greater than or equal to range start.");
    }
}
