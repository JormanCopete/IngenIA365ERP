using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Commands.CreatePayrollDeductionConcept;

public record CreatePayrollDeductionConceptCommand : IRequest<Result<Guid>>
{
    public string CompanyCode { get; init; } = string.Empty;
    public string BranchId { get; init; } = string.Empty;
    public string CostCenterId { get; init; } = string.Empty;
    public int CreditLineId { get; init; }
    public string PayrollConceptCode { get; init; } = string.Empty;
    public string InterestConceptCode { get; init; } = string.Empty;
    public string ExtraConceptCode { get; init; } = string.Empty;
}

public class CreatePayrollDeductionConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePayrollDeductionConceptCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePayrollDeductionConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PayrollDeductionConcept
        {
            CompanyCode = request.CompanyCode,
            BranchId = request.BranchId,
            CostCenterId = request.CostCenterId,
            CreditLineId = request.CreditLineId,
            PayrollConceptCode = request.PayrollConceptCode,
            InterestConceptCode = request.InterestConceptCode,
            ExtraConceptCode = request.ExtraConceptCode,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PayrollDeductionConcepts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePayrollDeductionConceptCommandValidator : AbstractValidator<CreatePayrollDeductionConceptCommand>
{
    public CreatePayrollDeductionConceptCommandValidator()
    {
        RuleFor(x => x.CompanyCode)
            .NotEmpty().WithMessage("Company code is required.")
            .MaximumLength(5).WithMessage("Company code must not exceed 5 characters.");

        RuleFor(x => x.BranchId)
            .MaximumLength(5).WithMessage("Branch ID must not exceed 5 characters.");

        RuleFor(x => x.CostCenterId)
            .MaximumLength(10).WithMessage("Cost center ID must not exceed 10 characters.");

        RuleFor(x => x.PayrollConceptCode)
            .MaximumLength(8).WithMessage("Payroll concept code must not exceed 8 characters.");

        RuleFor(x => x.InterestConceptCode)
            .MaximumLength(8).WithMessage("Interest concept code must not exceed 8 characters.");

        RuleFor(x => x.ExtraConceptCode)
            .MaximumLength(8).WithMessage("Extra concept code must not exceed 8 characters.");
    }
}
