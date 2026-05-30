using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.TerminateEmployee;

public record TerminateEmployeeCommand(
    Guid EmployeePublicId,
    DateTime TerminationDate,
    string? TerminationCause) : IRequest<Result>;

public class TerminateEmployeeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<TerminateEmployeeCommand, Result>
{
    public async Task<Result> Handle(TerminateEmployeeCommand request, CancellationToken ct)
    {
        // 1. Find employee
        var employee = await context.Employees.FirstOrDefaultAsync(
            e => e.PublicId == request.EmployeePublicId && !e.IsDeleted, ct);
        if (employee is null)
            return Result.Failure(new Error("Employee.NotFound",
                "Empleado no encontrado."));

        // 2. Validate not already terminated
        if (employee.Status == -1)
            return Result.Failure(new Error("Employee.AlreadyTerminated",
                "El empleado ya fue retirado."));

        // 3. Soft-terminate employee
        employee.Status = -1;
        employee.TerminationDate = request.TerminationDate;
        employee.TerminationCause = request.TerminationCause ?? "";
        employee.UpdatedAt = dateTime.UtcNow;
        employee.UpdatedBy = currentUser.UserName;

        // 4. Mark person as no longer employee
        var person = await context.People.FirstOrDefaultAsync(
            p => p.Id == employee.PersonId && !p.IsDeleted, ct);
        if (person is not null)
        {
            person.IsEmployee = false;
            person.UpdatedAt = dateTime.UtcNow;
            person.UpdatedBy = currentUser.UserName;
        }

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
