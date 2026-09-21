using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;

/// <summary>
/// Cómo se registra un empleado, en un solo sitio (feature 008). Recibe la persona ya
/// resuelta —existente y rastreada, o recién agregada por <c>PersonFactory</c> y todavía sin
/// Id— y deja en el contexto la ficha, su primer cambio de salario y la bandera
/// <c>IsEmployee</c> encendida, <b>sin guardar</b>. Las tres filas se enlazan por navegación
/// (<c>Employee.Person</c>, <c>SalaryChange.Employee</c>), así que un solo
/// <c>SaveChangesAsync</c> las inserta en orden y en una transacción: o quedan las tres o
/// ninguna. Hasta el 2026-09-13 <c>RegisterEmployeeCommand</c> guardaba dos veces para
/// conocer el Id de la ficha antes de escribir el cambio de salario.
/// </summary>
public sealed class EmployeeRegistrar(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
{
    public async Task<Result<Employee>> PrepareAsync(Person person, EmployeeInput input, CancellationToken ct)
    {
        // Una sola ficha viva por persona. Una persona nueva (Id 0) no puede tener ninguna;
        // una retirada (Status -1) no bloquea: el reingreso es una ficha nueva (FR-017).
        if (person.Id != 0)
        {
            var yaEsEmpleado = await context.Employees.AsNoTracking()
                .AnyAsync(e => e.PersonId == person.Id && !e.IsDeleted && e.Status != -1, ct);
            if (yaEsEmpleado)
                return Result.Failure<Employee>(new Error("Employee.AlreadyExists",
                    "Esta persona ya esta registrada como empleado activo."));
        }

        var healthInsuranceId = await IdDeAsync(() => context.HealthInsuranceProviders, input.HealthInsurancePublicId, ct);
        var pensionId = await IdDeAsync(() => context.PensionProviders, input.PensionProviderPublicId, ct);
        var workRiskId = await IdDeAsync(() => context.WorkRiskProviders, input.WorkRiskProviderPublicId, ct);
        var severanceFundId = await IdDeAsync(() => context.SeveranceProviders, input.SeveranceProviderPublicId, ct);
        var familySubsidyId = await IdDeAsync(() => context.FamilyCompensationFunds, input.FamilyCompensationFundPublicId, ct);
        var payrollBankId = await IdDeAsync(() => context.Banks, input.PayrollBankPublicId, ct);
        // Feature 010: banco de dispersion (FK real a COR_Banks); si no viene, el mismo de la banca de nomina.
        var disbursementBankId = input.DisbursementBankPublicId is null ? payrollBankId : await IdDeAsync(() => context.Banks, input.DisbursementBankPublicId, ct);

        // La clase ARL sí se exige cuando viene: sin la fila la liquidación queda bloqueada
        // con «sin clase de riesgo ARL» y nadie sabría por qué (2026-09-11).
        var workRiskRateId = 0;
        if (input.WorkRiskRatePublicId is { } clase)
        {
            workRiskRateId = await IdDeAsync(() => context.WorkRiskRates, clase, ct);
            if (workRiskRateId == 0)
                return Result.Failure<Employee>(new Error("Payroll.WorkRiskRateNotFound",
                    "La clase de riesgo ARL elegida no existe."));
        }

        // Plan de nómina (feature 005): todo empleado nace en el plan por defecto de la
        // cooperativa. Sin esto queda con PayrollPlanId = 0, fuera de cualquier período:
        // no admite novedades ni entra en la liquidación. Cambiar de plan es
        // ChangeEmployeePlanCommand, con fecha de efecto.
        var plan = input.PayrollPlanPublicId is { } planElegido
            ? await context.PayrollPlans.AsNoTracking()
                .Where(p => p.PublicId == planElegido && p.IsActive && !p.IsDeleted)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct)
            : await context.PayrollPlans.AsNoTracking()
                .Where(p => p.IsDefault && p.IsActive && !p.IsDeleted)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);
        if (input.PayrollPlanPublicId is not null && plan is null)
            return Result.Failure<Employee>(new Error("Payroll.PlanNotFound",
                "El plan de nómina elegido no existe o está inactivo."));

        // IMPORTANTE: el DDL de PAY_Employees tiene varias columnas legacy NOT NULL
        // sin DEFAULT (AreaCode, SectionId, TerminationCause, PensionFundMember, etc.).
        // Si EF Core pasa NULL en cualquiera de esas, SQL Server rechaza el INSERT.
        // Por eso inicializamos TODOS los strings nullable en cadena vacia y las
        // fechas obligatorias en DateTime.MaxValue.
        var employee = new Employee
        {
            Person = person,
            PersonId = person.Id,
            PayrollCompanyId = 1,
            PayrollPlanId = plan ?? 0,
            CostCenterId = "",
            AreaCode = "",
            SectionId = "",
            TerminationCause = "",
            PensionFundMember = "",
            IsLiquidated = "",
            SpecialRegime = "",
            ExtraBonusFlag = "",
            Salary = input.BaseSalary,
            SalaryType = 0,
            ContractType = input.ContractType,
            JoinDate = input.HireDate,
            HealthInsuranceId = healthInsuranceId,
            PensionFundId = pensionId,
            WorkRiskId = workRiskId,
            WorkRiskRateId = workRiskRateId,
            SeveranceFundId = severanceFundId,
            FamilySubsidyId = familySubsidyId,
            PayrollBankId = payrollBankId == 0 ? "" : payrollBankId.ToString(),
            PayrollBankAccountNumber = input.PayrollBankAccountNumber ?? "",
            PayrollBankAccountType = input.PayrollBankAccountType,
            // Fechas legacy (sentinel del SOLIDO original). RehireDate no se usa: el
            // reingreso es una ficha nueva, no una fecha en la retirada (FR-017).
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
        var reparoDeFicha = FichaPilaDian.Aplicar(employee, input, disbursementBankId);
        if (reparoDeFicha is not null)
            return Result.Failure<Employee>(reparoDeFicha);

        context.Employees.Add(employee);

        // Registro inicial en historial salarial, enlazado por navegación: EF pone el
        // EmployeeId al insertar. UserName es la columna legada de SOLIDO: varchar(20).
        // Un correo como usuario no cabe y PostgreSQL rechaza el INSERT entero (22001).
        // Quién lo hizo de verdad va en CreatedBy, sin recorte.
        context.SalaryChanges.Add(new SalaryChange
        {
            Employee = employee,
            PayrollCompanyId = 1,
            EffectiveDate = input.HireDate,
            NewSalary = input.BaseSalary,
            UserName = SalaryChange.RecortarUsuario(currentUser.UserName),
            EntryDate = dateTime.UtcNow,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        });

        // La bandera derivada la escribe quien crea la fila hija (Principio V).
        person.IsEmployee = true;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        return Result.Success(employee);
    }

    /// <summary>
    /// Id interno de un catálogo por su PublicId; 0 si no viene o no existe (como hacía el
    /// handler). El catálogo se pide con una función para no tocarlo cuando no viene nada.
    /// </summary>
    private static async Task<int> IdDeAsync<T>(Func<IQueryable<T>> catalogo, Guid? publicId, CancellationToken ct)
        where T : Domain.Common.BaseEntity
    {
        if (publicId is not { } id) return 0;
        return await catalogo().AsNoTracking()
            .Where(x => x.PublicId == id && !x.IsDeleted)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }
}
