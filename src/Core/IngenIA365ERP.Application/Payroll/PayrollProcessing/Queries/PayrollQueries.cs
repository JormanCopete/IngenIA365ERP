using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayrollProcessing.Queries;

// --- DTOs ---

public record PayrollSummaryDto(
    Guid PeriodPublicId,
    string PeriodName,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal TotalNet,
    int EmployeeCount,
    int PeriodStatus,
    string StatusMessage);

public record PayrollEmployeeDetailDto(
    Guid EmployeePublicId,
    string EmployeeName,
    string IdentificationNumber,
    decimal Earnings,
    decimal Deductions,
    decimal Net,
    List<PayrollConceptLineDto> Concepts);

public record PayrollConceptLineDto(
    string ConceptName,
    int ConceptCode,
    decimal Amount,
    string Nature); // "D"=devengado, "D"=deduccion

public record PayslipDto(
    Guid EmployeePublicId,
    string EmployeeName,
    string IdentificationNumber,
    string PeriodName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal BaseSalary,
    List<PayslipLineDto> EarningLines,
    List<PayslipLineDto> DeductionLines,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPay);

public record PayslipLineDto(
    string ConceptName,
    int ConceptCode,
    decimal Amount);

// --- List Payroll Summary ---

public record ListPayrollSummaryQuery(Guid PayPeriodPublicId) : IRequest<Result<PayrollSummaryDto>>;

public class ListPayrollSummaryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPayrollSummaryQuery, Result<PayrollSummaryDto>>
{
    public async Task<Result<PayrollSummaryDto>> Handle(
        ListPayrollSummaryQuery request, CancellationToken ct)
    {
        var period = await context.PayPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PayPeriodPublicId && !p.IsDeleted, ct);
        if (period is null)
            return Result.Failure<PayrollSummaryDto>(new Error("Payroll.PeriodNotFound",
                "Periodo no encontrado."));

        var transactions = await context.PayrollTransactions.AsNoTracking()
            .Where(t => t.PayPeriodId == period.Id && !t.IsDeleted)
            .ToListAsync(ct);

        var earnings = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount ?? 0);
        var deductions = transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount ?? 0));
        var net = earnings - deductions;
        var employeeCount = transactions.Select(t => t.EmployeeId).Distinct().Count();

        return Result.Success(new PayrollSummaryDto(
            period.PublicId,
            period.Description ?? $"Periodo {period.PeriodId}",
            earnings,
            deductions,
            net,
            employeeCount,
            (int)period.Status,
            period.StatusMessage));
    }
}

// --- Get Payroll Detail ---

public record GetPayrollDetailQuery(Guid PayPeriodPublicId, Guid? EmployeePublicId)
    : IRequest<Result<List<PayrollEmployeeDetailDto>>>;

public class GetPayrollDetailQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollDetailQuery, Result<List<PayrollEmployeeDetailDto>>>
{
    public async Task<Result<List<PayrollEmployeeDetailDto>>> Handle(
        GetPayrollDetailQuery request, CancellationToken ct)
    {
        var period = await context.PayPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PayPeriodPublicId && !p.IsDeleted, ct);
        if (period is null)
            return Result.Failure<List<PayrollEmployeeDetailDto>>(new Error("Payroll.PeriodNotFound",
                "Periodo no encontrado."));

        var txQuery = context.PayrollTransactions.AsNoTracking()
            .Where(t => t.PayPeriodId == period.Id && !t.IsDeleted);

        if (request.EmployeePublicId.HasValue)
        {
            var emp = await context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId.Value, ct);
            if (emp is not null)
                txQuery = txQuery.Where(t => t.EmployeeId == emp.Id);
        }

        var transactions = await txQuery.ToListAsync(ct);

        // Load employee names — vienen de COR_People via JOIN.
        var employeeIds = transactions.Select(t => t.EmployeeId).Distinct().ToList();
        var employees = await (
            from e in context.Employees.AsNoTracking()
            join p in context.People.AsNoTracking() on e.PersonId equals p.Id
            where employeeIds.Contains(e.Id)
            select new { e.Id, e.PublicId, Name = p.FirstName + " " + p.LastName, IdentificationNumber = p.TaxId })
            .ToDictionaryAsync(x => x.Id, x => new { x.PublicId, x.Name, x.IdentificationNumber }, ct);

        // Load concept names
        var conceptIds = transactions.Select(t => t.ConceptId).Distinct().ToList();
        var concepts = await context.PayrollConcepts.AsNoTracking()
            .Where(c => conceptIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Name, c.ConceptCode, c.Nature }, ct);

        var result = transactions
            .GroupBy(t => t.EmployeeId)
            .Select(g =>
            {
                var emp = employees.GetValueOrDefault(g.Key);
                var conceptLines = g.Select(t =>
                {
                    var concept = concepts.GetValueOrDefault(t.ConceptId);
                    return new PayrollConceptLineDto(
                        concept?.Name ?? t.Description,
                        concept?.ConceptCode ?? 0,
                        t.Amount ?? 0,
                        (t.Amount ?? 0) >= 0 ? "D" : "C");
                }).ToList();

                var earnings = conceptLines.Where(c => c.Amount >= 0).Sum(c => c.Amount);
                var deductions = conceptLines.Where(c => c.Amount < 0).Sum(c => Math.Abs(c.Amount));

                return new PayrollEmployeeDetailDto(
                    emp?.PublicId ?? Guid.Empty,
                    emp?.Name ?? "",
                    emp?.IdentificationNumber ?? "",
                    earnings,
                    deductions,
                    earnings - deductions,
                    conceptLines);
            })
            .OrderBy(d => d.EmployeeName)
            .ToList();

        return Result.Success(result);
    }
}

// --- Get Payslip ---

public record GetPayslipQuery(Guid PayPeriodPublicId, Guid EmployeePublicId)
    : IRequest<Result<PayslipDto>>;

public class GetPayslipQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayslipQuery, Result<PayslipDto>>
{
    public async Task<Result<PayslipDto>> Handle(
        GetPayslipQuery request, CancellationToken ct)
    {
        var period = await context.PayPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PayPeriodPublicId && !p.IsDeleted, ct);
        if (period is null)
            return Result.Failure<PayslipDto>(new Error("Payslip.PeriodNotFound",
                "Periodo no encontrado."));

        var employee = await context.Employees.AsNoTracking()
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId && !e.IsDeleted, ct);
        if (employee is null)
            return Result.Failure<PayslipDto>(new Error("Payslip.EmployeeNotFound",
                "Empleado no encontrado."));

        var transactions = await context.PayrollTransactions.AsNoTracking()
            .Where(t => t.PayPeriodId == period.Id
                     && t.EmployeeId == employee.Id
                     && !t.IsDeleted)
            .ToListAsync(ct);

        var conceptIds = transactions.Select(t => t.ConceptId).Distinct().ToList();
        var concepts = await context.PayrollConcepts.AsNoTracking()
            .Where(c => conceptIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Name, c.ConceptCode }, ct);

        var earningLines = transactions
            .Where(t => (t.Amount ?? 0) > 0)
            .Select(t =>
            {
                var c = concepts.GetValueOrDefault(t.ConceptId);
                return new PayslipLineDto(c?.Name ?? t.Description, c?.ConceptCode ?? 0, t.Amount ?? 0);
            }).ToList();

        var deductionLines = transactions
            .Where(t => (t.Amount ?? 0) < 0)
            .Select(t =>
            {
                var c = concepts.GetValueOrDefault(t.ConceptId);
                return new PayslipLineDto(c?.Name ?? t.Description, c?.ConceptCode ?? 0, Math.Abs(t.Amount ?? 0));
            }).ToList();

        var totalEarnings = earningLines.Sum(l => l.Amount);
        var totalDeductions = deductionLines.Sum(l => l.Amount);

        return Result.Success(new PayslipDto(
            employee.PublicId,
            $"{employee.Person.FirstName} {employee.Person.LastName}",
            employee.Person.TaxId,
            period.Description ?? $"Periodo {period.PeriodId}",
            DateOnly.FromDateTime(period.StartDate),
            DateOnly.FromDateTime(period.EndDate),
            employee.Salary,
            earningLines,
            deductionLines,
            totalEarnings,
            totalDeductions,
            totalEarnings - totalDeductions));
    }
}
