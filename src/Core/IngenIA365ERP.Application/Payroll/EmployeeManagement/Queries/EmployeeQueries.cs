using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
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

public record EmployeeDetailDto(
    Guid PublicId,
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
    List<SalaryHistoryDto> SalaryHistory,
    List<RecentPayrollEntryDto> RecentEntries);

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

public record GetEmployeeByPersonIdQuery(Guid PersonPublicId) : IRequest<Result<EmployeeDetailDto>>;

public class GetEmployeeByPersonIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetEmployeeByPersonIdQuery, Result<EmployeeDetailDto>>
{
    public async Task<Result<EmployeeDetailDto>> Handle(GetEmployeeByPersonIdQuery request, CancellationToken ct)
    {
        var employee = await context.Employees.AsNoTracking()
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.Person.PublicId == request.PersonPublicId && !e.IsDeleted, ct);

        if (employee is null)
            return Result.Failure<EmployeeDetailDto>(new Error("Employee.NotFound",
                "Esta persona aun no tiene el rol empleado."));

        // Reusa el mismo helper interno como GetEmployeeByIdQueryHandler
        return await new GetEmployeeByIdQueryHandler(context).Handle(
            new GetEmployeeByIdQuery(employee.PublicId), ct);
    }
}

// --- Get Employee By Id ---

public record GetEmployeeByIdQuery(Guid PublicId) : IRequest<Result<EmployeeDetailDto>>;

public class GetEmployeeByIdQueryHandler(IApplicationDbContext context)
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
            salaryHistory,
            recentEntries));
    }
}
