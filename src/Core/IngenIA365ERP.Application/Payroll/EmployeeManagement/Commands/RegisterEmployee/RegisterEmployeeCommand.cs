using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;

public record RegisterEmployeeCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public string PositionName { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public decimal BaseSalary { get; init; }
    public int ContractType { get; init; }
    public DateTime HireDate { get; init; }
    public Guid? HealthInsurancePublicId { get; init; }
    public Guid? PensionProviderPublicId { get; init; }
    public Guid? WorkRiskProviderPublicId { get; init; }
    public string? BankAccountNumber { get; init; }
    public Guid? BankPublicId { get; init; }
}

public class RegisterEmployeeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RegisterEmployeeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterEmployeeCommand request, CancellationToken ct)
    {
        // 1. Resolve Person
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("Employee.PersonNotFound",
                "Persona no encontrada."));

        // 2. Check not already an employee
        var existingEmployee = await context.Employees.FirstOrDefaultAsync(
            e => e.PersonId == person.Id && !e.IsDeleted && e.Status != -1, ct);
        if (existingEmployee is not null)
            return Result.Failure<Guid>(new Error("Employee.AlreadyExists",
                "Esta persona ya esta registrada como empleado activo."));

        // 3. Resolve Health Insurance
        int healthInsuranceId = 0;
        if (request.HealthInsurancePublicId.HasValue)
        {
            var eps = await context.HealthInsuranceProviders.AsNoTracking()
                .FirstOrDefaultAsync(h => h.PublicId == request.HealthInsurancePublicId.Value && !h.IsDeleted, ct);
            if (eps is not null) healthInsuranceId = eps.Id;
        }

        // 4. Resolve Pension Provider
        int pensionId = 0;
        if (request.PensionProviderPublicId.HasValue)
        {
            var pension = await context.PensionProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PensionProviderPublicId.Value && !p.IsDeleted, ct);
            if (pension is not null) pensionId = pension.Id;
        }

        // 5. Resolve Work Risk Provider
        int workRiskId = 0;
        if (request.WorkRiskProviderPublicId.HasValue)
        {
            var wrl = await context.WorkRiskProviders.AsNoTracking()
                .FirstOrDefaultAsync(w => w.PublicId == request.WorkRiskProviderPublicId.Value && !w.IsDeleted, ct);
            if (wrl is not null) workRiskId = wrl.Id;
        }

        // 6. Resolve Bank
        string bankId = "";
        if (request.BankPublicId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.BankPublicId.Value && !b.IsDeleted, ct);
            if (bank is not null) bankId = bank.Id.ToString();
        }

        // 7. Create Employee
        var employee = new Employee
        {
            PersonId = person.Id,
            PayrollCompanyId = 1, // default company
            LastName = person.LastName,
            FirstName = person.FirstName,
            IdentificationNumber = person.TaxId,
            Salary = request.BaseSalary,
            ContractType = request.ContractType,
            JoinDate = request.HireDate,
            BirthDate = person.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue,
            HealthInsuranceId = healthInsuranceId,
            PensionFundId = pensionId,
            WorkRiskId = workRiskId,
            BankId = bankId,
            BankAccountNumber = request.BankAccountNumber ?? "",
            Status = 1, // Active
            Address = person.Address ?? "",
            Email = person.Email ?? "",
            Phone = person.Phone1 ?? "",
            Mobile = person.Mobile ?? "",
            CityId = person.CityId ?? 0,
            CostCenterId = "",
            IssuedAt = person.IdIssuedAt ?? "",
            TerminationDate = DateTime.MaxValue,
            TerminationCause = "",
            ContractEndDate = DateTime.MaxValue,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.Employees.Add(employee);

        // 8. Mark person as employee
        person.IsEmployee = true;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        // 9. Record initial salary change
        var salaryChange = new SalaryChange
        {
            PayrollCompanyId = 1,
            EmployeeId = employee.Id, // will be set after SaveChanges
            EffectiveDate = request.HireDate,
            NewSalary = request.BaseSalary,
            UserName = currentUser.UserName,
            EntryDate = dateTime.UtcNow,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        await context.SaveChangesAsync(ct);

        // Now set the FK for salary change
        salaryChange.EmployeeId = employee.Id;
        context.SalaryChanges.Add(salaryChange);
        await context.SaveChangesAsync(ct);

        return Result.Success(employee.PublicId);
    }
}

public class RegisterEmployeeCommandValidator : AbstractValidator<RegisterEmployeeCommand>
{
    public RegisterEmployeeCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("Persona requerida.");

        RuleFor(x => x.BaseSalary)
            .GreaterThan(0).WithMessage("Salario base debe ser mayor a 0.");

        RuleFor(x => x.HireDate)
            .NotEmpty().WithMessage("Fecha de ingreso requerida.");

        RuleFor(x => x.ContractType)
            .InclusiveBetween(0, 10).WithMessage("Tipo de contrato invalido.");
    }
}
