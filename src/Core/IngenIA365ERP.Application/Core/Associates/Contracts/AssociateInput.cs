using FluentValidation;

namespace IngenIA365ERP.Application.Core.Associates.Contracts;

/// <summary>
/// Datos de la afiliación de un asociado (feature 008): la parte de
/// <c>RegisterAssociateCommand</c> que no es la persona. Lo comparten el registro sobre una
/// persona existente y el alta en un paso (<c>RegisterAssociateWithPersonCommand</c>).
/// </summary>
public record AssociateInput
{
    // Afiliacion
    public DateOnly? JoinDate { get; init; }
    public decimal ContributionRate { get; init; }
    public Guid? EmployerCompanyPublicId { get; init; }
    public Guid? BranchPublicId { get; init; }
    public Guid? SectionPublicId { get; init; }
    public Guid? CommitteePublicId { get; init; }
    public string? CategoryRating { get; init; }
    public string? AssociateClass { get; init; }
    public string? PaymentType { get; init; }
    public decimal ContributionPledged { get; init; }

    // Empleo externo
    public string? ExternalEmployerName { get; init; }
    public DateOnly? ExternalEmploymentStartDate { get; init; }
    public decimal ExternalSalary { get; init; }
    public string? ExternalSalaryType { get; init; }
    public decimal ExternalSeverance { get; init; }
    public string? ExternalSeveranceFund { get; init; }

    // Banca deposito
    public Guid? DepositBankPublicId { get; init; }
    public string? DepositBankAccountNumber { get; init; }
    public string? DepositBankAccountType { get; init; }

    // Conyuge laboral
    public string? SpouseEmployer { get; init; }
    public decimal SpouseSalary { get; init; }
    public string? SpousePosition { get; init; }
    public string? SpouseProfession { get; init; }
}

public class AssociateInputValidator : AbstractValidator<AssociateInput>
{
    public AssociateInputValidator()
    {
        RuleFor(x => x.ContributionRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExternalSalary).GreaterThanOrEqualTo(0);
    }
}
