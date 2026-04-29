using FluentValidation;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

public class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(150).WithMessage("Last name must not exceed 150 characters.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(150).WithMessage("First name must not exceed 150 characters.");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("Tax ID (NIT/Cedula) is required.")
            .MaximumLength(20).WithMessage("Tax ID must not exceed 20 characters.");

        RuleFor(x => x.IdType)
            .NotEmpty().WithMessage("ID type is required.")
            .MaximumLength(2).WithMessage("ID type must not exceed 2 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("A valid email address is required.")
            .MaximumLength(120);

        RuleFor(x => x.Phone1)
            .MaximumLength(40);

        RuleFor(x => x.Mobile)
            .MaximumLength(30);

        RuleFor(x => x.Address)
            .MaximumLength(120);

        RuleFor(x => x.BusinessName)
            .MaximumLength(150);

        RuleFor(x => x.Gender)
            .MaximumLength(2);

        RuleFor(x => x.MaritalStatus)
            .MaximumLength(2);

        RuleFor(x => x.EducationLevel)
            .MaximumLength(2);

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Date of birth must be in the past.");
    }
}
