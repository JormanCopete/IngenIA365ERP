using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.CostCenters.Commands.CreateCostCenter;

public record CreateCostCenterCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? CompanyTaxId { get; init; }
    public short PayrollType { get; init; }
    public short Period { get; init; }
    public short PayrollPeriodicity { get; init; }
    public string? PayrollStatus { get; init; }
}

public class CreateCostCenterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCostCenterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCostCenterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new CostCenter
        {
            Name = request.Name,
            CompanyName = request.CompanyName,
            CompanyTaxId = request.CompanyTaxId,
            PayrollType = request.PayrollType,
            Period = request.Period,
            PayrollPeriodicity = request.PayrollPeriodicity,
            PayrollStatus = request.PayrollStatus,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CostCenters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCostCenterCommandValidator : AbstractValidator<CreateCostCenterCommand>
{
    public CreateCostCenterCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.CompanyName)
            .MaximumLength(100).WithMessage("Company name must not exceed 100 characters.");

        RuleFor(x => x.CompanyTaxId)
            .MaximumLength(20).WithMessage("Company tax ID must not exceed 20 characters.");

        RuleFor(x => x.PayrollStatus)
            .MaximumLength(30).WithMessage("Payroll status must not exceed 30 characters.");
    }
}
