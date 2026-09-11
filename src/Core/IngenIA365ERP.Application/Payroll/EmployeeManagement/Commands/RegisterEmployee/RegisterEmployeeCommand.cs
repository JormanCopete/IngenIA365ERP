using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;

/// <summary>
/// Registra a una persona EXISTENTE como empleado interno de la cooperativa.
/// La Person debe existir previamente (ver /maestros/personas).
///
/// <para>
/// Solo guarda datos LABORALES en PAY_Employees. Los datos personales
/// (nombre, documento, contacto) se leen de COR_People via PersonId.
/// </para>
///
/// <para>
/// Si la persona ya es asociado, conserva su fila en COR_Associates intacta:
/// los salarios externo (Associate.ExternalSalary) e interno (Employee.Salary)
/// son INDEPENDIENTES.
/// </para>
/// </summary>
public record RegisterEmployeeCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }

    // Datos laborales
    public decimal BaseSalary { get; init; }
    public int ContractType { get; init; }
    public DateTime HireDate { get; init; }

    // Lookups (resueltos a Id en el handler)
    public Guid? HealthInsurancePublicId { get; init; }
    public Guid? PensionProviderPublicId { get; init; }
    public Guid? WorkRiskProviderPublicId { get; init; }

    /// <summary>
    /// Clase de riesgo ARL (fila de <c>PAY_WorkRiskRates</c>). Sin ella el motor no
    /// puede calcular el aporte a riesgos laborales y la liquidación queda bloqueada
    /// con «Sin clase de riesgo ARL registrada en la ficha».
    /// </summary>
    public Guid? WorkRiskRatePublicId { get; init; }

    // Banca nomina
    public Guid? PayrollBankPublicId { get; init; }
    public string? PayrollBankAccountNumber { get; init; }
    public int PayrollBankAccountType { get; init; }
}

public class RegisterEmployeeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RegisterEmployeeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterEmployeeCommand request, CancellationToken ct)
    {
        // 1. Persona existente
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("Employee.PersonNotFound",
                "Persona no encontrada."));

        // 2. No registrarla dos veces como empleado activo
        var existingEmployee = await context.Employees.FirstOrDefaultAsync(
            e => e.PersonId == person.Id && !e.IsDeleted && e.Status != -1, ct);
        if (existingEmployee is not null)
            return Result.Failure<Guid>(new Error("Employee.AlreadyExists",
                "Esta persona ya esta registrada como empleado activo."));

        // 3. Resolver lookups
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

        string payrollBankId = "";
        if (request.PayrollBankPublicId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.PayrollBankPublicId.Value && !b.IsDeleted, ct);
            if (bank is not null) payrollBankId = bank.Id.ToString();
        }

        // 3b. Plan de nómina (feature 005): todo empleado nace en el plan por defecto de la
        // cooperativa. Sin esto queda con PayrollPlanId = 0, fuera de cualquier período:
        // no admite novedades ni entra en la liquidación. Cambiar de plan es
        // ChangeEmployeePlanCommand, con fecha de efecto.
        var planPorDefecto = await context.PayrollPlans.AsNoTracking()
            .Where(p => p.IsDefault && p.IsActive && !p.IsDeleted)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        // 4. Crear empleado.
        // IMPORTANTE: el DDL de PAY_Employees tiene varias columnas legacy NOT NULL
        // sin DEFAULT (AreaCode, SectionId, TerminationCause, PensionFundMember, etc.).
        // Si EF Core pasa NULL en cualquiera de esas, SQL Server rechaza el INSERT.
        // Por eso inicializamos TODOS los strings nullable en cadena vacia y las
        // fechas obligatorias en DateTime.MaxValue.
        var employee = new Employee
        {
            PersonId = person.Id,
            PayrollCompanyId = 1,
            PayrollPlanId = planPorDefecto ?? 0,
            CostCenterId = "",
            AreaCode = "",
            SectionId = "",
            TerminationCause = "",
            PensionFundMember = "",
            IsLiquidated = "",
            SpecialRegime = "",
            ExtraBonusFlag = "",
            // Datos laborales reales del comando
            Salary = request.BaseSalary,
            SalaryType = 0,
            ContractType = request.ContractType,
            JoinDate = request.HireDate,
            HealthInsuranceId = healthInsuranceId,
            PensionFundId = pensionId,
            WorkRiskId = workRiskId,
            WorkRiskRateId = workRiskRateId,
            // Banca nomina
            PayrollBankId = payrollBankId,
            PayrollBankAccountNumber = request.PayrollBankAccountNumber ?? "",
            PayrollBankAccountType = request.PayrollBankAccountType,
            // Fechas legacy (sentinel del SOLIDO original)
            TerminationDate = DateTime.MaxValue,
            ContractEndDate = DateTime.MaxValue,
            RehireDate = DateTime.MaxValue,
            SeveranceCauseDate = DateTime.MaxValue,
            LicenseExpiryDate = DateTime.MaxValue,
            VacationCauseDate = DateTime.MaxValue,
            BonusCauseDate = DateTime.MaxValue,
            Status = 1,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.Employees.Add(employee);

        // 5. Marcar el flag IsEmployee en la persona
        person.IsEmployee = true;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        // 6. Registro inicial en historial salarial.
        // UserName es la columna legada de SOLIDO: varchar(20). Un correo como usuario no
        // cabe y PostgreSQL rechaza el INSERT entero (22001). Quién lo hizo de verdad va en
        // CreatedBy, sin recorte; aquí sólo se conserva el prefijo por compatibilidad.
        var salaryChange = new SalaryChange
        {
            PayrollCompanyId = 1,
            EmployeeId = 0,
            EffectiveDate = request.HireDate,
            NewSalary = request.BaseSalary,
            UserName = SalaryChange.RecortarUsuario(currentUser.UserName),
            EntryDate = dateTime.UtcNow,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        await context.SaveChangesAsync(ct);

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
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Persona requerida.");
        RuleFor(x => x.BaseSalary).GreaterThan(0).WithMessage("Salario base debe ser mayor a 0.");
        RuleFor(x => x.HireDate).NotEmpty().WithMessage("Fecha de ingreso requerida.");
        RuleFor(x => x.ContractType).InclusiveBetween(0, 10).WithMessage("Tipo de contrato invalido.");
        RuleFor(x => x.PayrollBankAccountType).InclusiveBetween(0, 2);
    }
}
