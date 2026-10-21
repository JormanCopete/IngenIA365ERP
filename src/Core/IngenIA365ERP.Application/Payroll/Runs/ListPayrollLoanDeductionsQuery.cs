using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs;

/// <summary>El empleado de un descuento de Cartera: por <c>PublicId</c> y nombre, nunca por Id interno (Principio VI). (nuevo)</summary>
public sealed record PayrollLoanDeductionEmployeeDto(Guid PublicId, string Name, string Document);

/// <summary>
/// Un descuento de Cartera de la nómina ordinaria (feature 013, T0166; contracts/api.md §24, data-model parte 3 §7.1): lo esperado,
/// lo descontado, lo no descontado y su motivo, el estado frente al contrato y los comprobantes de Cartera. (nuevo)
/// </summary>
public sealed record PayrollLoanDeductionDto(
    PayrollLoanDeductionEmployeeDto Employee,
    Guid ObligationPublicId,
    string ObligationNumber,
    decimal Expected,
    decimal Deducted,
    decimal NotDeducted,
    string? Reason,
    PayrollLoanDeductionStatus Status,
    Guid? CarteraReceiptPublicId,
    Guid? CarteraReversalPublicId)
{
    /// <summary>Lo que Cartera aplicó al aprobar (nulo mientras sea borrador).</summary>
    public decimal? Applied { get; init; }

    /// <summary>La respuesta del contrato, en español (sin códigos).</summary>
    public string? ResultMessage { get; init; }
}

/// <summary><c>GET /api/payroll/runs/{runId}/loan-deductions</c> (<c>Payroll.Runs.View</c>): los recaudos y no descontados de Cartera de una corrida. (nuevo)</summary>
public sealed record ListPayrollLoanDeductionsQuery(Guid RunPublicId) : IRequest<Result<IReadOnlyList<PayrollLoanDeductionDto>>>;

public sealed class ListPayrollLoanDeductionsQueryValidator : AbstractValidator<ListPayrollLoanDeductionsQuery>
{
    public ListPayrollLoanDeductionsQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ListPayrollLoanDeductionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPayrollLoanDeductionsQuery, Result<IReadOnlyList<PayrollLoanDeductionDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollLoanDeductionDto>>> Handle(ListPayrollLoanDeductionsQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Where(r => r.PublicId == request.RunPublicId).Select(r => new { r.Id }).FirstOrDefaultAsync(ct);
        if (run is null) return Result.Failure<IReadOnlyList<PayrollLoanDeductionDto>>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var filas = await (
            from d in db.PayrollLoanDeductions.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on d.PayrollRunEmployeeId equals re.Id
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where d.PayrollRunId == run.Id
            select new { d, e.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId })
            .ToListAsync(ct);

        IReadOnlyList<PayrollLoanDeductionDto> dtos = filas
            .Select(x => new PayrollLoanDeductionDto(
                new PayrollLoanDeductionEmployeeDto(x.PublicId, NombreDePersona.Completo(x.FirstName, x.OtherNames, x.LastName, x.SecondLastName), x.TaxId),
                x.d.ObligationPublicId, x.d.ObligationNumber, x.d.ExpectedAmount, x.d.DeductedAmount, x.d.NotDeductedAmount, x.d.NotDeductedReason,
                x.d.Status, x.d.CarteraReceiptPublicId, x.d.CarteraReversalPublicId)
            {
                Applied = x.d.AppliedAmount,
                ResultMessage = x.d.ResultMessage,
            })
            .OrderBy(x => x.Employee.Name, StringComparer.CurrentCulture).ThenBy(x => x.ObligationNumber, StringComparer.Ordinal)
            .ToList();
        return Result.Success(dtos);
    }
}
