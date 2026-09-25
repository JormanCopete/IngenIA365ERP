using FluentValidation;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployeeWithPerson;

/// <summary>
/// Persona nueva y empleado en un solo paso (feature 008, US1, FR-001). Es <b>un</b> comando —un
/// evento de auditoría con el request completo (FR-014)— y <b>un</b> <c>SaveChangesAsync</c>: la
/// persona, la ficha, su primer cambio de salario y la bandera <c>IsEmployee</c> quedan los
/// cuatro o ninguno. Las reglas son las mismas que en <c>CreatePersonCommand</c> y
/// <c>RegisterEmployeeCommand</c> porque comparten <see cref="PersonFactory"/> y
/// <see cref="EmployeeRegistrar"/>.
/// </summary>
/// <remarks>Feature 012 (T46, T175): <paramref name="Authorization"/> es la autorización de datos del titular, opcional.</remarks>
public sealed record RegisterEmployeeWithPersonCommand(PersonInput Person, EmployeeInput Employee, AutorizacionAlCrear? Authorization = null)
    : IRequest<Result<RegisterEmployeeWithPersonResult>>;

public sealed record RegisterEmployeeWithPersonResult(Guid PersonPublicId, Guid EmployeePublicId);

public sealed class RegisterEmployeeWithPersonCommandHandler(
    AltaConAutorizacion altas,
    EmployeeRegistrar empleados)
    : IRequestHandler<RegisterEmployeeWithPersonCommand, Result<RegisterEmployeeWithPersonResult>>
{
    public async Task<Result<RegisterEmployeeWithPersonResult>> Handle(
        RegisterEmployeeWithPersonCommand request, CancellationToken ct)
    {
        var alta = await altas.GuardarAsync(request.Person, request.Authorization,
            persona => empleados.PrepareAsync(persona, request.Employee, ct), ct);
        return alta.IsSuccess
            ? Result.Success(new RegisterEmployeeWithPersonResult(alta.Value.Persona.PublicId, alta.Value.Rol.PublicId))
            : Result.Failure<RegisterEmployeeWithPersonResult>(alta.Error);
    }
}

public sealed class RegisterEmployeeWithPersonCommandValidator : AbstractValidator<RegisterEmployeeWithPersonCommand>
{
    public RegisterEmployeeWithPersonCommandValidator()
    {
        RuleFor(x => x.Person).NotNull().WithMessage("Faltan los datos de la persona.")
            .SetValidator(new PersonInputValidator());
        RuleFor(x => x.Employee).NotNull().WithMessage("Faltan los datos laborales.")
            .SetValidator(new EmployeeInputValidator());
        RuleFor(x => x.Authorization!).SetValidator(new AutorizacionAlCrearValidator()).When(x => x.Authorization is not null);
    }
}
