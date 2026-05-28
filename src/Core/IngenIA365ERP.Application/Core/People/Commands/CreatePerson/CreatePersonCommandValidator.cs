using FluentValidation;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

public class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Apellidos obligatorios.")
            .MaximumLength(150);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Nombres obligatorios.")
            .MaximumLength(150);

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("NIT/Cedula obligatorio.")
            .MaximumLength(20);

        RuleFor(x => x.IdType)
            .NotEmpty().WithMessage("Tipo de identificacion obligatorio.")
            .MaximumLength(2);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Correo electronico no valido.")
            .MaximumLength(120);

        RuleFor(x => x.Phone1).MaximumLength(40);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.Address).MaximumLength(120);
        RuleFor(x => x.BusinessName).MaximumLength(150);
        RuleFor(x => x.Gender).MaximumLength(2);
        RuleFor(x => x.MaritalStatus).MaximumLength(2);
        RuleFor(x => x.EducationLevel).MaximumLength(2);

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("La fecha de nacimiento debe ser anterior a hoy.");
    }
}
