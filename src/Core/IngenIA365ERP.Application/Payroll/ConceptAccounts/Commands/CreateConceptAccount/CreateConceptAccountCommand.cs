using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.ConceptAccounts.Commands.CreateConceptAccount;

public record CreateConceptAccountCommand : IRequest<Result<Guid>>
{
    public int ConceptId { get; init; }
    public string CostCenterId { get; init; } = string.Empty;
    public string ExpenseAccountCode { get; init; } = string.Empty;
    public string CounterAccountCode { get; init; } = string.Empty;
    public string ProvisionAccountCode { get; init; } = string.Empty;
}

public class CreateConceptAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateConceptAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateConceptAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ConceptAccount
        {
            ConceptId = request.ConceptId,
            CostCenterId = request.CostCenterId,
            ExpenseAccountCode = request.ExpenseAccountCode,
            CounterAccountCode = request.CounterAccountCode,
            ProvisionAccountCode = request.ProvisionAccountCode,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.ConceptAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateConceptAccountCommandValidator : AbstractValidator<CreateConceptAccountCommand>
{
    public CreateConceptAccountCommandValidator()
    {
        RuleFor(x => x.CostCenterId)
            .MaximumLength(10).WithMessage("CostCenterId must not exceed 10 characters.");

        RuleFor(x => x.ExpenseAccountCode)
            .MaximumLength(15).WithMessage("ExpenseAccountCode must not exceed 15 characters.");

        RuleFor(x => x.CounterAccountCode)
            .MaximumLength(15).WithMessage("CounterAccountCode must not exceed 15 characters.");

        RuleFor(x => x.ProvisionAccountCode)
            .MaximumLength(15).WithMessage("ProvisionAccountCode must not exceed 15 characters.");
    }
}
