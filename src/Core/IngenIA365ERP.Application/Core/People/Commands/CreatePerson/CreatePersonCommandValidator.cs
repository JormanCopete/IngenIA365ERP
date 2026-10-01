using FluentValidation;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

/// <summary>Las reglas viven en <see cref="PersonInputValidator"/>; aquí sólo se incluyen.</summary>
public class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        Include(new PersonInputValidator());
        RuleFor(x => x.Authorization!).SetValidator(new AutorizacionAlCrearValidator()).When(x => x.Authorization is not null);
    }
}
