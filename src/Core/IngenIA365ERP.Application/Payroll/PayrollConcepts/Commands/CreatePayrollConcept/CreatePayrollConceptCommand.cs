using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.PayrollConcepts.Commands.CreatePayrollConcept;

public record CreatePayrollConceptCommand : IRequest<Result<Guid>>
{
    public int ConceptCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int ConceptClass { get; init; }
    public int Nature { get; init; }
    public decimal Value { get; init; }
    public decimal Factor { get; init; }
    public int Base { get; init; }
    public int AffectsSalary { get; init; }
    public string? DaysComputed { get; init; }
    public int LiquidationBase { get; init; }
    public decimal TopSalary { get; init; }
    public int AffectsBenefits { get; init; }
    public int AffectsWithholding { get; init; }
    public int IsBenefit { get; init; }
    public decimal ProvisionRate { get; init; }
    public int ProvisionBase { get; init; }
    public int AffectsSeverance { get; init; }
    public int AffectsBonus { get; init; }
    public int AffectsVacation { get; init; }
    public int AffectsIndemnity { get; init; }
    public int ConceptSubClass { get; init; }
}

public class CreatePayrollConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePayrollConceptCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePayrollConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PayrollConcept
        {
            ConceptCode = request.ConceptCode,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            ConceptClass = request.ConceptClass,
            Nature = request.Nature,
            Value = request.Value,
            Factor = request.Factor,
            Base = request.Base,
            AffectsSalary = request.AffectsSalary,
            DaysComputed = request.DaysComputed ?? string.Empty,
            LiquidationBase = request.LiquidationBase,
            TopSalary = request.TopSalary,
            AffectsBenefits = request.AffectsBenefits,
            AffectsWithholding = request.AffectsWithholding,
            IsBenefit = request.IsBenefit,
            ProvisionRate = request.ProvisionRate,
            ProvisionBase = request.ProvisionBase,
            AffectsSeverance = request.AffectsSeverance,
            AffectsBonus = request.AffectsBonus,
            AffectsVacation = request.AffectsVacation,
            AffectsIndemnity = request.AffectsIndemnity,
            ConceptSubClass = request.ConceptSubClass,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PayrollConcepts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePayrollConceptCommandValidator : AbstractValidator<CreatePayrollConceptCommand>
{
    public CreatePayrollConceptCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");

        RuleFor(x => x.DaysComputed)
            .MaximumLength(2).WithMessage("DaysComputed must not exceed 2 characters.");
    }
}
