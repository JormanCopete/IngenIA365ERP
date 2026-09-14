using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;

/// <summary>
/// Registra a una persona EXISTENTE como empleado interno de la cooperativa. Para
/// persona nueva y empleado en un solo paso está <c>RegisterEmployeeWithPersonCommand</c>
/// (feature 008); los dos comparten <see cref="EmployeeRegistrar"/>.
///
/// <para>
/// Solo guarda datos LABORALES en PAY_Employees. Los datos personales
/// (nombre, documento, contacto) se leen de COR_People via PersonId.
/// </para>
///
/// <para>
/// Si la persona ya es asociado, conserva su fila en COR_Associates intacta:
/// los salarios externo (Associate.ExternalSalary) e interno (Employee.Salary)
/// son INDEPENDIENTES, y la bandera <c>IsAssociate</c> no se toca.
/// </para>
/// </summary>
public record RegisterEmployeeCommand : EmployeeInput, IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
}

public class RegisterEmployeeCommandHandler(
    IApplicationDbContext context,
    EmployeeRegistrar empleados)
    : IRequestHandler<RegisterEmployeeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterEmployeeCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("Employee.PersonNotFound",
                "Persona no encontrada."));

        var preparado = await empleados.PrepareAsync(person, request, ct);
        if (preparado.IsFailure)
            return Result.Failure<Guid>(preparado.Error);

        // Un solo SaveChanges: ficha, primer cambio de salario y bandera, o nada.
        await context.SaveChangesAsync(ct);
        return Result.Success(preparado.Value.PublicId);
    }
}

public class RegisterEmployeeCommandValidator : AbstractValidator<RegisterEmployeeCommand>
{
    public RegisterEmployeeCommandValidator()
    {
        Include(new EmployeeInputValidator());
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Persona requerida.");
    }
}
