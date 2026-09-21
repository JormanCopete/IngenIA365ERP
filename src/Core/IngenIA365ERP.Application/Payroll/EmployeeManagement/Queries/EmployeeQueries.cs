using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeManagement.Queries;

// --- DTOs ---

public record EmployeeDto(
    Guid PublicId,
    string FullName,
    string IdentificationNumber,
    string PositionName,
    decimal Salary,
    DateTime HireDate,
    string StatusText,
    string Email);

public record SalaryHistoryDto(
    DateTime EffectiveDate,
    decimal NewSalary,
    string? UserName);

public record RecentPayrollEntryDto(
    string ConceptName,
    decimal? Amount,
    string PeriodDescription);

// Feature 010 (contracts/api.md §12): los bloques que la ficha suma.

/// <summary>Lo que la PILA lee de la ficha. <c>SalaryTypeCode</c>: F fijo, V variable, X integral.</summary>
public sealed record EmployeePilaDto(
    string? ContributorType,
    string? ContributorSubtype,
    string? DivipolaDepartment,
    string? DivipolaMunicipality,
    string? EconomicActivityCode,
    string? WorkCenter,
    string SalaryTypeCode,
    bool ForeignNotPensionObligated,
    bool ColombianAbroad,
    PensionTransitionRegime PensionTransitionRegime,
    bool HighRiskPension);

/// <summary>Lo que el documento DIAN lee de la ficha (D-05: tipo y subtipo son los del cotizante PILA).</summary>
public sealed record EmployeeDianDto(
    string? WorkerType,
    string? WorkerSubtype,
    DianContractType? ContractTypeDian,
    bool HighRiskPension,
    string? PaymentMethodCode,
    string? WorkAddress);

/// <summary>Saldo de vacaciones derivado (R6). Lo calcula la US4; hasta entonces la ficha lo trae nulo.</summary>
public sealed record EmployeeVacationBalanceDto(decimal PendingDays, DateOnly AsOf);

/// <summary>El saldo inicial de prestaciones vigente (R3) tal como lo verá el motor.</summary>
public sealed record EmployeeOpeningBalanceDto(
    Guid PublicId,
    OpeningBalanceKind Kind,
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    string? EnteredBy,
    DateTime EnteredAt,
    Guid? ConsumedByRunPublicId);

/// <summary>El porcentaje fijo del procedimiento 2 vigente hoy y de dónde salió (manual o calculado).</summary>
public sealed record EmployeeWithholdingRateDto(decimal RatePercent, DateTime ValidFrom, DateTime? ValidTo, WithholdingRateOrigin Origin, byte Procedure);

/// <summary>La terminación viva del contrato (feature 010, R7), si la hay.</summary>
public sealed record EmployeeTerminationDto(
    Guid PublicId,
    DateOnly TerminationDate,
    string ReasonCode,
    string ReasonName,
    bool GeneratesSeverancePay,
    TerminationStatus Status);

public record EmployeeDetailDto(
    Guid PublicId,
    // Feature 008: la persona a la que pertenece la ficha. La pantalla la usaba para buscar la
    // persona por documento en /search y quedarse con el primer resultado.
    Guid PersonPublicId,
    // Datos personales: vienen de COR_People via JOIN
    string FirstName,
    string LastName,
    string IdentificationNumber,
    string Email,
    string Phone,
    string Mobile,
    string Address,
    // Datos laborales internos: vienen de PAY_Employees
    decimal Salary,
    int ContractType,
    DateTime HireDate,
    DateTime? TerminationDate,
    string TerminationCause,
    int Status,
    string HealthInsuranceName,
    string PensionProviderName,
    string WorkRiskProviderName,
    string PayrollBankAccountNumber,
    int PayrollBankAccountType,
    Guid? HealthInsurancePublicId,
    Guid? PensionProviderPublicId,
    Guid? WorkRiskProviderPublicId,
    Guid? PayrollBankPublicId,
    string? PayrollBankName,
    // Clase de riesgo ARL (fila de PAY_WorkRiskRates): sin ella no se calcula el aporte.
    Guid? WorkRiskRatePublicId,
    string WorkRiskRateName,
    Guid? SeveranceProviderPublicId,
    string SeveranceProviderName,
    Guid? FamilyCompensationFundPublicId,
    string FamilyCompensationFundName,
    Guid? PayrollPlanPublicId,
    string PayrollPlanName,
    DateTime? PayrollPlanEffectiveFrom,
    List<SalaryHistoryDto> SalaryHistory,
    List<RecentPayrollEntryDto> RecentEntries,
    // Feature 010 (contracts/api.md §12)
    string EmployeeClass = "Standard",
    EmployeePilaDto? Pila = null,
    EmployeeDianDto? Dian = null,
    ApprenticeStage? ApprenticeStage = null,
    Guid? DisbursementBankPublicId = null,
    string? DisbursementBankName = null,
    string? DisbursementBankTransferCode = null,
    EmployeeVacationBalanceDto? VacationBalance = null,
    EmployeeOpeningBalanceDto? OpeningBalance = null,
    EmployeeWithholdingRateDto? CurrentWithholdingRate = null,
    EmployeeTerminationDto? Termination = null);

// --- List Employees ---

public record ListEmployeesQuery : IRequest<Result<PagedList<EmployeeDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public bool? ActiveOnly { get; init; }
}

public class ListEmployeesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListEmployeesQuery, Result<PagedList<EmployeeDto>>>
{
    public async Task<Result<PagedList<EmployeeDto>>> Handle(
        ListEmployeesQuery request, CancellationToken ct)
    {
        // Datos personales (FullName, IdentificationNumber, Email) se leen de
        // COR_People via JOIN. PAY_Employees ya no los duplica.
        var query = from e in context.Employees.AsNoTracking().Where(e => !e.IsDeleted)
                    join p in context.People.AsNoTracking() on e.PersonId equals p.Id
                    where !p.IsDeleted
                    select new { e, p };

        if (request.ActiveOnly == true)
            query = query.Where(x => x.e.Status == 1);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.p.TaxId.Contains(search)
                || x.p.FirstName.Contains(search)
                || x.p.LastName.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.p.LastName)
            .ThenBy(x => x.p.FirstName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new EmployeeDto(
                x.e.PublicId,
                x.p.FirstName + " " + x.p.LastName,
                x.p.TaxId,
                "",
                x.e.Salary,
                x.e.JoinDate,
                x.e.Status == 1 ? "Activo" : x.e.Status == -1 ? "Retirado" : "Inactivo",
                x.p.Email ?? ""))
            .ToListAsync(ct);

        return Result.Success(new PagedList<EmployeeDto>(
            items, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Employee by Person Id (para detectar "ya es empleado" desde el buscador) ---

/// <summary>
/// La ficha <b>viva</b> de una persona (feature 008, FR-017). Una persona puede tener varias
/// fichas a lo largo del tiempo —cada reingreso tras un retiro crea una nueva y la retirada
/// queda como historial—, pero a lo sumo una con <c>Status != -1</c>. Hasta el 2026-09-13 se
/// tomaba la primera que apareciera, retirada o no, y la pantalla podía abrir la equivocada.
/// Sin ficha viva responde <c>Employee.NotFound</c>: la pantalla pasa a modo registro.
/// </summary>
public record GetEmployeeByPersonIdQuery(Guid PersonPublicId) : IRequest<Result<EmployeeDetailDto>>;

public class GetEmployeeByPersonIdQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetEmployeeByPersonIdQuery, Result<EmployeeDetailDto>>
{
    public async Task<Result<EmployeeDetailDto>> Handle(GetEmployeeByPersonIdQuery request, CancellationToken ct)
    {
        var employee = await context.Employees.AsNoTracking()
            .Where(e => e.Person.PublicId == request.PersonPublicId && !e.IsDeleted && e.Status != -1)
            .OrderByDescending(e => e.JoinDate)
            .FirstOrDefaultAsync(ct);

        if (employee is null)
            return Result.Failure<EmployeeDetailDto>(new Error("Employee.NotFound",
                "Esta persona no tiene una ficha de empleado vigente."));

        // Reusa el mismo helper interno como GetEmployeeByIdQueryHandler
        return await new GetEmployeeByIdQueryHandler(context, clock).Handle(
            new GetEmployeeByIdQuery(employee.PublicId), ct);
    }
}

// --- Get Employee By Id ---

public record GetEmployeeByIdQuery(Guid PublicId) : IRequest<Result<EmployeeDetailDto>>;

public class GetEmployeeByIdQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDetailDto>>
{
    public async Task<Result<EmployeeDetailDto>> Handle(
        GetEmployeeByIdQuery request, CancellationToken ct)
    {
        var employee = await context.Employees.AsNoTracking()
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, ct);

        if (employee is null)
            return Result.Failure<EmployeeDetailDto>(new Error("Employee.NotFound",
                "Empleado no encontrado."));

        var person = employee.Person;

        // Resolve provider names + PublicIds
        string epsName = "", pensionName = "", arlName = "", bankName = "", claseArlName = "", cesantiasName = "", cajaName = "";
        Guid? epsPublicId = null, pensionPublicId = null, arlPublicId = null, bankPublicId = null, claseArlPublicId = null, cesantiasPublicId = null, cajaPublicId = null;

        if (employee.HealthInsuranceId > 0)
        {
            var eps = await context.HealthInsuranceProviders.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == employee.HealthInsuranceId, ct);
            if (eps is not null) { epsName = eps.Name; epsPublicId = eps.PublicId; }
        }
        if (employee.PensionFundId > 0)
        {
            var pension = await context.PensionProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == employee.PensionFundId, ct);
            if (pension is not null) { pensionName = pension.Name; pensionPublicId = pension.PublicId; }
        }
        if (employee.WorkRiskId > 0)
        {
            var arl = await context.WorkRiskProviders.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == employee.WorkRiskId, ct);
            if (arl is not null) { arlName = arl.Name; arlPublicId = arl.PublicId; }
        }
        if (employee.WorkRiskRateId > 0)
        {
            var clase = await context.WorkRiskRates.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == employee.WorkRiskRateId, ct);
            if (clase is not null) { claseArlName = clase.Name; claseArlPublicId = clase.PublicId; }
        }
        if (employee.SeveranceFundId > 0)
        {
            var fondo = await context.SeveranceProviders.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == employee.SeveranceFundId, ct);
            if (fondo is not null) { cesantiasName = fondo.Name; cesantiasPublicId = fondo.PublicId; }
        }
        if (employee.FamilySubsidyId > 0)
        {
            var caja = await context.FamilyCompensationFunds.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == employee.FamilySubsidyId, ct);
            if (caja is not null) { cajaName = caja.Name; cajaPublicId = caja.PublicId; }
        }
        Guid? planPublicId = null; var planName = "";
        if (employee.PayrollPlanId > 0)
        {
            var plan = await context.PayrollPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == employee.PayrollPlanId, ct);
            if (plan is not null) { planPublicId = plan.PublicId; planName = plan.Name; }
        }
        if (!string.IsNullOrWhiteSpace(employee.PayrollBankId)
            && int.TryParse(employee.PayrollBankId, out var bankIntId) && bankIntId > 0)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bankIntId, ct);
            if (bank is not null) { bankName = bank.Name; bankPublicId = bank.PublicId; }
        }

        // Salary history
        var salaryHistory = await context.SalaryChanges.AsNoTracking()
            .Where(s => s.EmployeeId == employee.Id && !s.IsDeleted)
            .OrderByDescending(s => s.EffectiveDate)
            .Take(20)
            .Select(s => new SalaryHistoryDto(s.EffectiveDate, s.NewSalary, s.UserName))
            .ToListAsync(ct);

        // Feature 010: banco de dispersión, saldo inicial, porcentaje P2 vigente, terminación viva.
        Guid? dispersionPublicId = null; string? dispersionName = null, dispersionAch = null;
        if (employee.DisbursementBankId is { } bancoDispersionId)
        {
            var banco = await context.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bancoDispersionId, ct);
            if (banco is not null) { dispersionPublicId = banco.PublicId; dispersionName = banco.Name; dispersionAch = banco.TransferCode; }
        }

        var saldos = await context.EmployeeBenefitOpeningBalances.AsNoTracking()
            .Include(b => b.ConsumedByRun)
            .Where(b => b.EmployeeId == employee.Id && !b.IsDeleted)
            .ToListAsync(ct);
        var saldoVigente = saldos.OrderByDescending(b => b.AsOfDate).ThenByDescending(b => b.Id).FirstOrDefault();
        var openingBalance = saldoVigente is null ? null : new EmployeeOpeningBalanceDto(
            saldoVigente.PublicId, saldoVigente.Kind, saldoVigente.AsOfDate, saldoVigente.PendingVacationDays, saldoVigente.AccruedSeverance,
            saldoVigente.AccruedSeveranceInterest, saldoVigente.AccruedServiceBonus, saldoVigente.CreatedBy, saldoVigente.CreatedAt, saldoVigente.ConsumedByRun?.PublicId);

        var hoy = clock.UtcNow.Date;
        var tasa = await context.EmployeeWithholdingRates.AsNoTracking()
            .Where(r => r.EmployeeId == employee.Id && r.ValidFrom <= hoy && (r.ValidTo == null || r.ValidTo >= hoy))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);
        var currentRate = tasa is null ? null : new EmployeeWithholdingRateDto(tasa.RatePercent, tasa.ValidFrom, tasa.ValidTo, tasa.Origin, employee.WithholdingProcedure);

        var terminacion = await context.EmploymentTerminations.AsNoTracking()
            .Include(t => t.TerminationReason)
            .Where(t => t.EmployeeId == employee.Id && (t.Status == TerminationStatus.Registered || t.Status == TerminationStatus.Settled))
            .OrderByDescending(t => t.TerminationDate)
            .FirstOrDefaultAsync(ct);
        var termination = terminacion is null ? null : new EmployeeTerminationDto(
            terminacion.PublicId, terminacion.TerminationDate, terminacion.TerminationReason?.Code ?? "", terminacion.TerminationReason?.Name ?? "",
            terminacion.TerminationReason?.GeneratesSeverancePay ?? false, terminacion.Status);

        var (divipolaDepto, divipolaMun) = FichaPilaDian.Divipola(employee.WorkMunicipalityDaneCode);
        var pila = new EmployeePilaDto(employee.PilaContributorType, employee.PilaContributorSubType, divipolaDepto, divipolaMun,
            employee.EconomicActivityCode, employee.WorkCenterCode, FichaPilaDian.SalaryTypeCode(employee),
            employee.ForeignNotRequiredToContributePension, employee.ColombianAbroad, employee.PensionTransitionRegime, employee.HighRiskPension);
        var dian = new EmployeeDianDto(employee.PilaContributorType, employee.PilaContributorSubType, employee.DianContractType,
            employee.HighRiskPension, employee.DianPaymentMethodCode, employee.WorkAddress);

        // Recent payroll entries
        var recentEntries = await context.PayrollTransactions.AsNoTracking()
            .Where(t => t.EmployeeId == employee.Id && !t.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .Take(30)
            .Join(context.PayrollConcepts.AsNoTracking(),
                t => t.ConceptId, c => c.Id,
                (t, c) => new RecentPayrollEntryDto(c.Name, t.Amount, t.Description))
            .ToListAsync(ct);

        return Result.Success(new EmployeeDetailDto(
            employee.PublicId,
            person.PublicId,
            person.FirstName,
            person.LastName,
            person.TaxId,
            person.Email ?? "",
            person.Phone1 ?? "",
            person.Mobile ?? "",
            person.Address ?? "",
            employee.Salary,
            employee.ContractType,
            employee.JoinDate,
            employee.TerminationDate == DateTime.MaxValue ? null : employee.TerminationDate,
            employee.TerminationCause ?? "",
            employee.Status,
            epsName,
            pensionName,
            arlName,
            employee.PayrollBankAccountNumber ?? "",
            employee.PayrollBankAccountType,
            epsPublicId,
            pensionPublicId,
            arlPublicId,
            bankPublicId,
            bankName,
            claseArlPublicId,
            claseArlName,
            cesantiasPublicId,
            cesantiasName,
            cajaPublicId,
            cajaName,
            planPublicId,
            planName,
            employee.PayrollPlanEffectiveFrom,
            salaryHistory,
            recentEntries,
            employee.EmployeeClass.ToString(),
            pila,
            dian,
            employee.ApprenticeStage,
            dispersionPublicId,
            dispersionName,
            dispersionAch,
            // El saldo de vacaciones es derivado (causado + inicial - disfrutado - compensado) y lo suma la US4.
            null,
            openingBalance,
            currentRate,
            termination));
    }
}
