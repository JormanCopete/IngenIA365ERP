using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayrollProcessing.Commands.ProcessPayroll;

public record ProcessPayrollCommand(Guid PayPeriodPublicId) : IRequest<Result<Guid>>;

public class ProcessPayrollCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ProcessPayrollCommand, Result<Guid>>
{
    // Colombian payroll deduction rates (simplified)
    private const decimal HealthEmployeeRate = 0.04m;   // 4% employee health
    private const decimal PensionEmployeeRate = 0.04m;   // 4% employee pension
    private const decimal HealthEmployerRate = 0.0850m;  // 8.5% employer health
    private const decimal PensionEmployerRate = 0.12m;   // 12% employer pension

    public async Task<Result<Guid>> Handle(ProcessPayrollCommand request, CancellationToken ct)
    {
        // 1. Find PayPeriod and validate status = Open
        var period = await context.PayPeriods.FirstOrDefaultAsync(
            p => p.PublicId == request.PayPeriodPublicId && !p.IsDeleted, ct);
        if (period is null)
            return Result.Failure<Guid>(new Error("Payroll.PeriodNotFound",
                "Periodo de pago no encontrado."));
        if (period.Status != 0)
            return Result.Failure<Guid>(new Error("Payroll.PeriodNotOpen",
                $"El periodo no esta abierto. Estado actual: {period.StatusMessage}"));

        // 2. Load all active employees for this payroll company
        var employees = await context.Employees
            .Where(e => e.PayrollCompanyId == period.PayrollCompanyId
                     && e.Status == 1 && !e.IsDeleted)
            .ToListAsync(ct);

        if (employees.Count == 0)
            return Result.Failure<Guid>(new Error("Payroll.NoEmployees",
                "No hay empleados activos para liquidar."));

        // 3. Load all payroll concepts
        var concepts = await context.PayrollConcepts.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToDictionaryAsync(c => c.ConceptCode, ct);

        // 4. Get the next sequence number for transactions
        decimal nextSeq = await context.PayrollTransactions
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.SequenceNumber)
            .Select(t => t.SequenceNumber)
            .FirstOrDefaultAsync(ct) + 1;

        decimal totalEarnings = 0, totalDeductions = 0, totalNet = 0;

        // 5. Process each employee
        foreach (var employee in employees)
        {
            // a. Load variable/one-time entries for this period
            var entries = await context.PayrollEntries.AsNoTracking()
                .Where(e => e.EmployeeId == employee.Id
                         && e.Cycle == period.PeriodId
                         && !e.IsDeleted)
                .ToListAsync(ct);

            // b. Calculate earnings
            decimal baseSalary = employee.Salary;
            decimal periodDays = period.CycleMonth.HasValue && period.CycleMonth > 0 ? period.CycleMonth.Value : 30;
            decimal dailySalary = baseSalary / 30m; // Colombian standard: 30-day month
            decimal periodSalary = dailySalary * periodDays;

            decimal extraEarnings = entries.Sum(e => e.IncapacityAmount); // extra entries amount
            decimal totalEmployeeEarnings = periodSalary + extraEarnings;

            // c. Calculate deductions
            decimal healthDeduction = totalEmployeeEarnings * HealthEmployeeRate;
            decimal pensionDeduction = totalEmployeeEarnings * PensionEmployeeRate;

            // Simplified withholding tax calculation (table-based in production)
            decimal withholdingTax = 0;
            if (employee.WithholdingTaxRate.HasValue && employee.WithholdingTaxRate > 0)
                withholdingTax = totalEmployeeEarnings * (employee.WithholdingTaxRate.Value / 100m);

            decimal totalEmployeeDeductions = healthDeduction + pensionDeduction + withholdingTax;
            decimal netPay = totalEmployeeEarnings - totalEmployeeDeductions;

            totalEarnings += totalEmployeeEarnings;
            totalDeductions += totalEmployeeDeductions;
            totalNet += netPay;

            // d. Create PayrollTransaction per concept per employee

            // Salary transaction
            var salaryTxn = CreateTransaction(period, employee, nextSeq++,
                1, periodSalary, "Salario basico");
            context.PayrollTransactions.Add(salaryTxn);

            // Extra entries transactions
            foreach (var entry in entries)
            {
                var extraTxn = CreateTransaction(period, employee, nextSeq++,
                    entry.EntityCode, entry.IncapacityAmount,
                    $"Novedad periodo {period.PeriodId}");
                context.PayrollTransactions.Add(extraTxn);
            }

            // Health deduction
            if (healthDeduction > 0)
            {
                var healthTxn = CreateTransaction(period, employee, nextSeq++,
                    0, -healthDeduction, "Descuento EPS empleado");
                context.PayrollTransactions.Add(healthTxn);
            }

            // Pension deduction
            if (pensionDeduction > 0)
            {
                var pensionTxn = CreateTransaction(period, employee, nextSeq++,
                    0, -pensionDeduction, "Descuento pension empleado");
                context.PayrollTransactions.Add(pensionTxn);
            }

            // Withholding tax
            if (withholdingTax > 0)
            {
                var withTxn = CreateTransaction(period, employee, nextSeq++,
                    0, -withholdingTax, "Retencion en la fuente");
                context.PayrollTransactions.Add(withTxn);
            }

            // Net pay transaction
            var netTxn = CreateTransaction(period, employee, nextSeq++,
                0, netPay, "Neto a pagar");
            context.PayrollTransactions.Add(netTxn);
        }

        // 6. Create AccountingDocument for payroll
        var voucherType = await context.VoucherTypes.FirstOrDefaultAsync(
            v => v.Code == "NM" && !v.IsDeleted, ct);

        // If "NM" voucher doesn't exist, try first available
        voucherType ??= await context.VoucherTypes
            .Where(v => !v.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (voucherType is not null)
        {
            var nextAccNum = voucherType.NextSequenceNumber + 1;
            voucherType.NextSequenceNumber = nextAccNum;
            var periodCode = period.StartDate.Year * 100 + period.StartDate.Month;

            var accDoc = new AccountingDocument
            {
                VoucherTypeCode = voucherType.Code,
                DocumentNumber = nextAccNum,
                Detail = $"Liquidacion nomina - {period.Description ?? $"Periodo {period.PeriodId}"}",
                TotalDebit = totalEarnings,
                TotalCredit = totalEarnings, // Deductions + Net = Earnings
                DocumentDate = DateOnly.FromDateTime(period.EndDate),
                IsClosed = false,
                IsVoided = false,
                PeriodCode = periodCode,
                ModuleCode = "NOM",
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.AccountingDocuments.Add(accDoc);
        }

        // 7. Mark PayPeriod as liquidated
        period.Status = 1; // Liquidated
        period.StatusMessage = $"Liquidado {dateTime.UtcNow:yyyy-MM-dd HH:mm} por {currentUser.UserName}. " +
            $"Empleados: {employees.Count}, Devengado: {totalEarnings:N2}, Deducido: {totalDeductions:N2}, Neto: {totalNet:N2}";
        period.UpdatedAt = dateTime.UtcNow;
        period.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success(period.PublicId);
    }

    private PayrollTransaction CreateTransaction(
        PayPeriod period, Employee employee, decimal seq,
        int conceptId, decimal amount, string description)
    {
        return new PayrollTransaction
        {
            PayPeriodId = period.Id,
            PayrollCompanyId = employee.PayrollCompanyId,
            EmployeeId = employee.Id,
            ConceptId = conceptId,
            SequenceNumber = seq,
            Amount = amount,
            TransactionDate = dateTime.UtcNow,
            Description = description,
            UserName = currentUser.UserName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
    }
}
