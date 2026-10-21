using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs;

/// <summary>
/// Un descuento de aportes o de ahorro de la nómina ordinaria (feature 014, T089; E-02): la cuenta, el concepto, lo esperado, lo descontado, lo no
/// descontado y su motivo, el estado frente al contrato de cuentas y los comprobantes de la Cartera. (nuevo)
/// </summary>
public sealed record PayrollAccountDeductionDto(
    PayrollLoanDeductionEmployeeDto Employee,
    Guid AccountPublicId,
    string AccountNumber,
    string ConceptCode,
    decimal Expected,
    decimal Deducted,
    decimal NotDeducted,
    string? Reason,
    PayrollLoanDeductionStatus Status,
    Guid? CarteraReceiptPublicId,
    Guid? CarteraReversalPublicId)
{
    /// <summary>Lo que la Cartera tomó al aprobar (nulo mientras sea borrador).</summary>
    public decimal? Applied { get; init; }

    /// <summary>La respuesta del contrato, en español.</summary>
    public string? ResultMessage { get; init; }
}

/// <summary><c>GET /api/payroll/runs/{runId}/account-deductions</c> (<c>Payroll.Runs.View</c>): los descuentos de aportes y de ahorro de una corrida. (nuevo)</summary>
public sealed record ListPayrollAccountDeductionsQuery(Guid RunPublicId) : IRequest<Result<IReadOnlyList<PayrollAccountDeductionDto>>>;

public sealed class ListPayrollAccountDeductionsQueryValidator : AbstractValidator<ListPayrollAccountDeductionsQuery>
{
    public ListPayrollAccountDeductionsQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ListPayrollAccountDeductionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPayrollAccountDeductionsQuery, Result<IReadOnlyList<PayrollAccountDeductionDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAccountDeductionDto>>> Handle(ListPayrollAccountDeductionsQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Where(r => r.PublicId == request.RunPublicId).Select(r => new { r.Id }).FirstOrDefaultAsync(ct);
        if (run is null) return Result.Failure<IReadOnlyList<PayrollAccountDeductionDto>>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var filas = await (
            from d in db.PayrollAccountDeductions.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on d.PayrollRunEmployeeId equals re.Id
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where d.PayrollRunId == run.Id
            select new { d, e.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId })
            .ToListAsync(ct);

        IReadOnlyList<PayrollAccountDeductionDto> dtos = filas
            .Select(x => new PayrollAccountDeductionDto(
                new PayrollLoanDeductionEmployeeDto(x.PublicId, NombreDePersona.Completo(x.FirstName, x.OtherNames, x.LastName, x.SecondLastName), x.TaxId),
                x.d.AccountPublicId, x.d.AccountNumber, x.d.ConceptCode, x.d.ExpectedAmount, x.d.DeductedAmount, x.d.NotDeductedAmount, x.d.NotDeductedReason,
                x.d.Status, x.d.CarteraReceiptPublicId, x.d.CarteraReversalPublicId)
            {
                Applied = x.d.AppliedAmount,
                ResultMessage = x.d.ResultMessage,
            })
            .OrderBy(x => x.Employee.Name, StringComparer.CurrentCulture).ThenBy(x => x.AccountNumber, StringComparer.Ordinal)
            .ToList();
        return Result.Success(dtos);
    }
}
