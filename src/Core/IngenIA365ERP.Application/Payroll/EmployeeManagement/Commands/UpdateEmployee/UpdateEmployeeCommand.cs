using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.UpdateEmployee;

/// <summary>
/// Actualiza datos LABORALES de un empleado interno.
/// Datos personales (nombre, contacto) NO se editan aqui — viven en
/// COR_People y se cambian desde /maestros/personas.
/// </summary>
public record UpdateEmployeeCommand : IRequest<Result>, IFichaPilaDian
{
    public Guid EmployeePublicId { get; init; }

    public decimal BaseSalary { get; init; }
    public int ContractType { get; init; }

    /// <summary>
    /// Fecha de ingreso (<c>PAY_Employees.JoinDate</c>). Hasta el 2026-09-18 no viajaba en el
    /// PUT y la pantalla la mostraba editable: se cambiaba y no cambiaba. Se puede corregir
    /// mientras no haya nada liquidado ni registrado que dependa de ella: una nómina aprobada
    /// de un período que empieza antes, una novedad en un período ya cerrado antes del nuevo
    /// ingreso o un cambio de salario anterior. El cambio de salario inicial —el que el alta
    /// deja en la fecha de ingreso— se mueve con ella. Nula, no cambia: es lo que mandan la
    /// ficha de detalle y las pruebas que sólo tocan otros campos.
    /// </summary>
    public DateTime? HireDate { get; init; }

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

    // Feature 010 (contracts/api.md §12). Un bloque que no viene no toca lo que había: las
    // pantallas anteriores a la feature mandan sólo lo de arriba y no deben borrar el DIVIPOLA.
    public PilaEmployeeInput? Pila { get; init; }
    public DianEmployeeInput? Dian { get; init; }
    public ApprenticeStage? ApprenticeStage { get; init; }
    /// <summary>Banco de dispersión. Nulo = no cambia; <see cref="ClearDisbursementBank"/> lo quita.</summary>
    public Guid? DisbursementBankPublicId { get; init; }
    public bool ClearDisbursementBank { get; init; }
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

        if (request.HireDate is { } ingreso)
        {
            var reparoDeIngreso = await CambiarIngresoAsync(employee, ingreso.Date, ct);
            if (reparoDeIngreso is not null) return Result.Failure(reparoDeIngreso);
        }

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

        int? disbursementBankId = null;
        if (request.ClearDisbursementBank) disbursementBankId = 0;
        else if (request.DisbursementBankPublicId is { } bancoDispersion)
        {
            var banco = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == bancoDispersion && !b.IsDeleted, ct);
            if (banco is null)
                return Result.Failure(new Error("Payroll.Employee.DisbursementBankNotFound", "El banco de dispersión elegido no existe."));
            disbursementBankId = banco.Id;
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
        var reparoDeFicha = FichaPilaDian.Aplicar(employee, request, disbursementBankId);
        if (reparoDeFicha is not null) return Result.Failure(reparoDeFicha);
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

    /// <summary>
    /// Mueve la fecha de ingreso si nada de lo ya registrado la contradice. Null si quedó
    /// (o no cambiaba); si no, el reparo con la fecha que lo impide.
    /// </summary>
    private async Task<Error?> CambiarIngresoAsync(Employee employee, DateTime ingreso, CancellationToken ct)
    {
        var anterior = employee.JoinDate.Date;
        if (ingreso == anterior) return null;

        // Una nómina aprobada (o reversada: también fue historia) cuyo período empieza antes de
        // la fecha más tardía de las dos calculó los días trabajados con el ingreso viejo.
        var tope = ingreso > anterior ? ingreso : anterior;
        var liquidado = await context.PayrollRunEmployees.AsNoTracking()
            .Where(re => re.EmployeeId == employee.Id
                && (re.Run!.Status == PayrollRunStatus.Approved || re.Run.Status == PayrollRunStatus.Reversed)
                && re.Run.PayPeriod!.StartDate < tope)
            .Select(re => (DateTime?)re.Run!.PayPeriod!.StartDate)
            .OrderBy(d => d)
            .FirstOrDefaultAsync(ct);
        if (liquidado is { } inicioLiquidado)
            return new Error("Employee.HireDateLocked",
                $"La fecha de ingreso no se puede cambiar: hay una nómina aprobada del período que empieza el {inicioLiquidado:dd/MM/yyyy}. Reversala primero.");

        // Una novedad activa en un período que termina antes del nuevo ingreso quedaría fuera de la vinculación.
        var novedadFuera = await context.PayrollNovelties.AsNoTracking()
            .Where(n => n.EmployeeId == employee.Id && n.Status == NoveltyStatus.Active && n.PayPeriod!.EndDate < ingreso)
            .Select(n => (DateTime?)n.PayPeriod!.EndDate)
            .OrderBy(d => d)
            .FirstOrDefaultAsync(ct);
        if (novedadFuera is { } finDeNovedad)
            return new Error("Employee.HireDateLocked",
                $"La fecha de ingreso no se puede cambiar al {ingreso:dd/MM/yyyy}: hay novedades en un período que termina el {finDeNovedad:dd/MM/yyyy}, antes del ingreso.");

        // El cambio de salario inicial se mueve con el ingreso; cualquier otro anterior lo impide.
        var cambios = await context.SalaryChanges
            .Where(c => c.EmployeeId == employee.Id)
            .OrderBy(c => c.EffectiveDate)
            .ToListAsync(ct);
        var inicial = cambios.FirstOrDefault(c => c.EffectiveDate.Date == anterior);
        var anteriorAlIngreso = cambios.FirstOrDefault(c => c != inicial && c.EffectiveDate.Date < ingreso);
        if (anteriorAlIngreso is not null)
            return new Error("Employee.HireDateLocked",
                $"La fecha de ingreso no se puede cambiar al {ingreso:dd/MM/yyyy}: hay un cambio de salario con efecto el {anteriorAlIngreso.EffectiveDate:dd/MM/yyyy}, antes del ingreso.");
        if (inicial is not null) inicial.EffectiveDate = ingreso;

        employee.JoinDate = ingreso;
        return null;
    }
}

public class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.BaseSalary).GreaterThan(0).WithMessage("El salario base debe ser mayor a 0.");
        RuleFor(x => x.HireDate).NotEqual(default(DateTime)).When(x => x.HireDate.HasValue).WithMessage("Fecha de ingreso inválida.");
        RuleFor(x => x.ContractType).InclusiveBetween(0, 10);
        RuleFor(x => x.PayrollBankAccountType).InclusiveBetween(0, 2);
        RuleFor(x => x.PayrollBankAccountNumber).MaximumLength(25);
        RuleFor(x => x.Pila!).SetValidator(new PilaEmployeeInputValidator()).When(x => x.Pila is not null);
        RuleFor(x => x.Dian!).SetValidator(new DianEmployeeInputValidator()).When(x => x.Dian is not null);
        RuleFor(x => x.ApprenticeStage).IsInEnum().When(x => x.ApprenticeStage is not null);
    }
}
