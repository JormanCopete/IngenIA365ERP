using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.UpdateEmployee;

/// <summary>
/// Actualiza datos LABORALES de un empleado interno.
/// Datos personales (nombre, contacto) NO se editan aqui — viven en
/// COR_People y se cambian desde /maestros/personas.
/// </summary>
public record UpdateEmployeeCommand : IRequest<Result>
{
    public Guid EmployeePublicId { get; init; }

    public decimal BaseSalary { get; init; }
    public int ContractType { get; init; }

    public Guid? HealthInsurancePublicId { get; init; }
    public Guid? PensionProviderPublicId { get; init; }
    public Guid? WorkRiskProviderPublicId { get; init; }
    /// <summary>Clase de riesgo ARL (fila de <c>PAY_WorkRiskRates</c>); ver <c>RegisterEmployeeCommand</c>.</summary>
    public Guid? WorkRiskRatePublicId { get; init; }
    /// <summary>Fondo de cesantías (fila de <c>PAY_SeveranceProviders</c>). No afecta la liquidación mensual; importa para la consignación anual y los reportes.</summary>
    public Guid? SeveranceProviderPublicId { get; init; }
    /// <summary>Caja de compensación familiar (fila de <c>PAY_FamilyCompensationFunds</c>).</summary>
    public Guid? FamilyCompensationFundPublicId { get; init; }

    public Guid? PayrollBankPublicId { get; init; }
    public string? PayrollBankAccountNumber { get; init; }
    public int PayrollBankAccountType { get; init; }
}

public class UpdateEmployeeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateEmployeeCommand, Result>
{
    public async Task<Result> Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        var employee = await context.Employees.FirstOrDefaultAsync(
            e => e.PublicId == request.EmployeePublicId && !e.IsDeleted, ct);
        if (employee is null)
            return Result.Failure(new Error("Employee.NotFound", "Empleado no encontrado."));

        if (employee.Status == -1)
            return Result.Failure(new Error("Employee.Terminated",
                "No se puede modificar un empleado retirado."));

        int healthInsuranceId = 0;
        if (request.HealthInsurancePublicId.HasValue)
        {
            var eps = await context.HealthInsuranceProviders.AsNoTracking()
                .FirstOrDefaultAsync(h => h.PublicId == request.HealthInsurancePublicId.Value && !h.IsDeleted, ct);
            if (eps is not null) healthInsuranceId = eps.Id;
        }

        int pensionId = 0;
        if (request.PensionProviderPublicId.HasValue)
        {
            var pension = await context.PensionProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PensionProviderPublicId.Value && !p.IsDeleted, ct);
            if (pension is not null) pensionId = pension.Id;
        }

        int workRiskId = 0;
        if (request.WorkRiskProviderPublicId.HasValue)
        {
            var wrl = await context.WorkRiskProviders.AsNoTracking()
                .FirstOrDefaultAsync(w => w.PublicId == request.WorkRiskProviderPublicId.Value && !w.IsDeleted, ct);
            if (wrl is not null) workRiskId = wrl.Id;
        }

        int workRiskRateId = 0;
        if (request.WorkRiskRatePublicId.HasValue)
        {
            var clase = await context.WorkRiskRates.AsNoTracking()
                .FirstOrDefaultAsync(r => r.PublicId == request.WorkRiskRatePublicId.Value && !r.IsDeleted, ct);
            if (clase is not null) workRiskRateId = clase.Id;
        }

        int severanceFundId = 0;
        if (request.SeveranceProviderPublicId.HasValue)
        {
            var fondo = await context.SeveranceProviders.AsNoTracking()
                .FirstOrDefaultAsync(f => f.PublicId == request.SeveranceProviderPublicId.Value && !f.IsDeleted, ct);
            if (fondo is not null) severanceFundId = fondo.Id;
        }

        int familySubsidyId = 0;
        if (request.FamilyCompensationFundPublicId.HasValue)
        {
            var caja = await context.FamilyCompensationFunds.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.FamilyCompensationFundPublicId.Value && !c.IsDeleted, ct);
            if (caja is not null) familySubsidyId = caja.Id;
        }

        string payrollBankId = "";
        if (request.PayrollBankPublicId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.PayrollBankPublicId.Value && !b.IsDeleted, ct);
            if (bank is not null) payrollBankId = bank.Id.ToString();
        }

        var previousSalary = employee.Salary;
        var salaryChanged = previousSalary != request.BaseSalary;

        employee.Salary = request.BaseSalary;
        employee.ContractType = request.ContractType;
        employee.HealthInsuranceId = healthInsuranceId;
        employee.PensionFundId = pensionId;
        employee.WorkRiskId = workRiskId;
        employee.WorkRiskRateId = workRiskRateId;
        employee.SeveranceFundId = severanceFundId;
        employee.FamilySubsidyId = familySubsidyId;
        employee.PayrollBankId = payrollBankId;
        employee.PayrollBankAccountNumber = request.PayrollBankAccountNumber ?? "";
        employee.PayrollBankAccountType = request.PayrollBankAccountType;
        employee.UpdatedAt = dateTime.UtcNow;
        employee.UpdatedBy = currentUser.UserName;

        if (salaryChanged)
        {
            var salaryChange = new SalaryChange
            {
                PayrollCompanyId = employee.PayrollCompanyId,
                EmployeeId = employee.Id,
                EffectiveDate = dateTime.UtcNow,
                NewSalary = request.BaseSalary,
                UserName = SalaryChange.RecortarUsuario(currentUser.UserName),
                EntryDate = dateTime.UtcNow,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.SalaryChanges.Add(salaryChange);
        }

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.BaseSalary).GreaterThan(0).WithMessage("El salario base debe ser mayor a 0.");
        RuleFor(x => x.ContractType).InclusiveBetween(0, 10);
        RuleFor(x => x.PayrollBankAccountType).InclusiveBetween(0, 2);
        RuleFor(x => x.PayrollBankAccountNumber).MaximumLength(25);
    }
}
