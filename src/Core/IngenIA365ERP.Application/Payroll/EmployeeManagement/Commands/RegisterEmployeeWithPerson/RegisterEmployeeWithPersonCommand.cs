using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployeeWithPerson;

/// <summary>
/// Persona nueva y empleado en un solo paso (feature 008, US1, FR-001). Es <b>un</b> comando —un
/// evento de auditoría con el request completo (FR-014)— y <b>un</b> <c>SaveChangesAsync</c>: la
/// persona, la ficha, su primer cambio de salario y la bandera <c>IsEmployee</c> quedan los
/// cuatro o ninguno. Las reglas son las mismas que en <c>CreatePersonCommand</c> y
/// <c>RegisterEmployeeCommand</c> porque comparten <see cref="PersonFactory"/> y
/// <see cref="EmployeeRegistrar"/>.
/// </summary>
public sealed record RegisterEmployeeWithPersonCommand(PersonInput Person, EmployeeInput Employee)
    : IRequest<Result<RegisterEmployeeWithPersonResult>>;

public sealed record RegisterEmployeeWithPersonResult(Guid PersonPublicId, Guid EmployeePublicId);

public sealed class RegisterEmployeeWithPersonCommandHandler(
    IApplicationDbContext context,
    PersonFactory personas,
    EmployeeRegistrar empleados)
    : IRequestHandler<RegisterEmployeeWithPersonCommand, Result<RegisterEmployeeWithPersonResult>>
{
    public async Task<Result<RegisterEmployeeWithPersonResult>> Handle(
        RegisterEmployeeWithPersonCommand request, CancellationToken ct)
    {
        var persona = await personas.PrepareAsync(request.Person, ct);
        if (persona.IsFailure)
            return Result.Failure<RegisterEmployeeWithPersonResult>(persona.Error);

        var empleado = await empleados.PrepareAsync(persona.Value, request.Employee, ct);
        if (empleado.IsFailure)
            return Result.Failure<RegisterEmployeeWithPersonResult>(empleado.Error);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PersonFactory.EsColisionDeDocumento(ex))
        {
            var colision = await personas.TraducirColisionAsync(ex, request.Person.TaxId, ct);
            return Result.Failure<RegisterEmployeeWithPersonResult>(colision!);
        }

        return Result.Success(new RegisterEmployeeWithPersonResult(persona.Value.PublicId, empleado.Value.PublicId));
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
    }
}
