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
    string FirstName,
    string LastName,
    string IdentificationNumber,
    string Email,
    string Phone,
    string Mobile,
    string Address,
    decimal Salary,
    int ContractType,
    DateTime HireDate,
    DateTime? TerminationDate,
    string TerminationCause,
    int Status,
    string HealthInsuranceName,
    string PensionProviderName,
    string WorkRiskProviderName,
    string BankAccountNumber,
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
        var query = context.Employees
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (request.ActiveOnly == true)
            query = query.Where(e => e.Status == 1);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(e =>
                e.IdentificationNumber.Contains(search)
                || e.FirstName.Contains(search)
                || e.LastName.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new EmployeeDto(
                e.PublicId,
                e.FirstName + " " + e.LastName,
                e.IdentificationNumber,
                "", // position resolved below
                e.Salary,
                e.JoinDate,
                e.Status == 1 ? "Activo" : e.Status == -1 ? "Retirado" : "Inactivo",
                e.Email))
            .ToListAsync(ct);

        return Result.Success(new PagedList<EmployeeDto>(
            items, totalCount, request.PageNumber, request.PageSize));
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
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, ct);

        if (employee is null)
            return Result.Failure<EmployeeDetailDto>(new Error("Employee.NotFound",
                "Empleado no encontrado."));

        // Resolve provider names
        string epsName = "", pensionName = "", arlName = "";
        if (employee.HealthInsuranceId > 0)
        {
            var eps = await context.HealthInsuranceProviders.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == employee.HealthInsuranceId, ct);
            epsName = eps?.Name ?? "";
        }
        if (employee.PensionFundId > 0)
        {
            var pension = await context.PensionProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == employee.PensionFundId, ct);
            pensionName = pension?.Name ?? "";
        }
        if (employee.WorkRiskId > 0)
        {
            var arl = await context.WorkRiskProviders.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == employee.WorkRiskId, ct);
            arlName = arl?.Name ?? "";
        }

        // Salary history
        var salaryHistory = await context.SalaryChanges.AsNoTracking()
            .Where(s => s.EmployeeId == employee.Id && !s.IsDeleted)
            .OrderByDescending(s => s.EffectiveDate)
            .Take(20)
            .Select(s => new SalaryHistoryDto(s.EffectiveDate, s.NewSalary, s.UserName))
            .ToListAsync(ct);

        // Recent payroll entries (last period)
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
            employee.FirstName,
            employee.LastName,
            employee.IdentificationNumber,
            employee.Email,
            employee.Phone,
            employee.Mobile,
            employee.Address,
            employee.Salary,
            employee.ContractType,
            employee.JoinDate,
            employee.TerminationDate == DateTime.MaxValue ? null : employee.TerminationDate,
            employee.TerminationCause,
            employee.Status,
            epsName,
            pensionName,
            arlName,
            employee.BankAccountNumber,
            salaryHistory,
            recentEntries));
    }
}
