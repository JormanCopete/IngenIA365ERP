using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>
/// Arma el documento de liquidación definitiva para firma (feature 010, FR-020, T066) desde la
/// corrida <c>Settlement</c>, la terminación y la ficha: empresa y NIT, empleado y documento, cargo,
/// fechas de ingreso y retiro, motivo con su base legal, tipo de contrato, salario base, cada rubro
/// con base y días, los descuentos con propuesto/aplicado/motivo, el neto y lo omitido con su razón.
/// Sobre un borrador la marca es «BORRADOR». No calcula nada: pinta lo que la corrida guardó.
/// </summary>
public sealed class SettlementDocumentModelBuilder(IApplicationDbContext db, ICurrentTenantService tenant, IDateTimeService clock)
{
    public async Task<Result<SettlementDocumentModel>> BuildAsync(Guid runPublicId, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.AccountingDocument).FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<SettlementDocumentModel>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement || run.TerminationId is null)
            return Result.Failure<SettlementDocumentModel>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));

        var terminacion = await db.EmploymentTerminations.AsNoTracking().Include(t => t.TerminationReason)
            .FirstOrDefaultAsync(t => t.Id == run.TerminationId, ct);
        if (terminacion is null) return Result.Failure<SettlementDocumentModel>(SettlementErrors.TerminationNotFound);

        var ficha = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where e.Id == terminacion.EmployeeId
            select new { e, p }).FirstOrDefaultAsync(ct);
        if (ficha is null) return Result.Failure<SettlementDocumentModel>(SettlementErrors.EmployeeNotFound);

        var fila = await db.PayrollRunEmployees.AsNoTracking().Include(f => f.Lines).FirstOrDefaultAsync(f => f.PayrollRunId == run.Id, ct);
        var lineas = fila?.Lines.OrderBy(l => l.Order).ToList() ?? [];
        var deducciones = await db.SettlementDeductions.AsNoTracking().Where(d => d.TerminationId == terminacion.Id).OrderBy(d => d.Kind).ThenBy(d => d.Id).ToListAsync(ct);
        var cargo = ficha.e.PositionId > 0 ? await db.Positions.AsNoTracking().Where(p => p.Id == ficha.e.PositionId).Select(p => p.Name).FirstOrDefaultAsync(ct) : null;

        var salarioBase = await db.SalaryChanges.AsNoTracking()
            .Where(s => s.EmployeeId == ficha.e.Id && s.EffectiveDate <= terminacion.TerminationDate.ToDateTime(TimeOnly.MinValue))
            .OrderByDescending(s => s.EffectiveDate).Select(s => (decimal?)s.NewSalary).FirstOrDefaultAsync(ct) ?? ficha.e.Salary;

        var notas = fila is null ? null : Notas(fila.NotesJson);
        var omitidos = notas?.Skips ?? [];

        SettlementDocumentLineModel Linea(Domain.Entities.Payroll.Transactions.PayrollRunLine l) =>
            new(l.ConceptCode, l.ConceptName, l.Nature, l.BaseAmount, l.Quantity, l.Amount, Resumen(l.ExplanationJson));

        var devengos = lineas.Where(l => l.Nature == ConceptNature.Earning).Select(Linea).ToList();
        var deduccionesDeLey = lineas.Where(l => l.Nature == ConceptNature.Deduction && l.SettlementDeductionId == null).Select(Linea).ToList();
        var descuentos = deducciones.Select(d => new SettlementDocumentDeductionModel(
            d.Description, d.ProposedAmount, d.AppliedAmount, d.AdjustmentReason,
            SettlementDeductionsReader.Item(d, run).RemainingAfter)).ToList();

        var nombre = NombreDePersona.Completo(ficha.p);
        var diasDeServicio = CalendarConventions.Days(ficha.e.JoinDate.Date, terminacion.TerminationDate.ToDateTime(TimeOnly.MinValue));

        return Result.Success(new SettlementDocumentModel(
            // Como el comprobante de pago: el nombre de la cooperativa lo da el tenant; el NIT no viaja en él.
            CooperativeName: tenant.TenantName ?? "Cooperativa",
            CooperativeTaxId: null,
            EmployeeName: nombre,
            EmployeeDocumentType: ficha.p.IdType,
            EmployeeDocument: ficha.p.TaxId,
            EmployeePosition: cargo,
            HireDate: ficha.e.JoinDate.Date,
            TerminationDate: terminacion.TerminationDate,
            ReasonName: terminacion.TerminationReason?.Name ?? string.Empty,
            ReasonLegalBasis: terminacion.TerminationReason?.LegalBasis,
            GeneratesSeverancePay: terminacion.TerminationReason?.GeneratesSeverancePay ?? false,
            ContractType: NombreContrato(terminacion.ContractTypeAtTermination ?? ficha.e.DianContractType),
            ContractEndDate: terminacion.ContractEndDate,
            BaseSalary: salarioBase,
            DaysOfService: diasDeServicio,
            Earnings: devengos,
            Deductions: deduccionesDeLey,
            PortfolioDeductions: descuentos,
            Omitted: omitidos,
            TotalEarnings: fila?.TotalEarnings ?? 0m,
            TotalDeductions: fila?.TotalDeductions ?? 0m,
            NetPay: fila?.NetPay ?? 0m,
            IsDraft: run.Status != PayrollRunStatus.Approved,
            RunVersion: run.Version,
            ApprovedAt: run.ApprovedAt,
            ApprovedBy: run.ApprovedBy,
            AccountingDocumentNumber: run.AccountingDocument?.Referencia(),
            GeneratedAt: clock.UtcNow));
    }

    public static string NombreContrato(DianContractType? tipo) => tipo switch
    {
        DianContractType.FixedTerm => "Término fijo",
        DianContractType.Indefinite => "Término indefinido",
        DianContractType.WorkOrLabor => "Obra o labor",
        DianContractType.Apprenticeship => "Contrato de aprendizaje",
        DianContractType.Internship => "Prácticas o pasantía",
        _ => "Sin registrar",
    };

    private static RunEmployeeNotes? Notas(string json)
    {
        try { return JsonSerializer.Deserialize<RunEmployeeNotes>(json, RunJson.Options); }
        catch (JsonException) { return null; }
    }

    private static string Resumen(string explanationJson)
    {
        try { return JsonSerializer.Deserialize<Explanation>(explanationJson, RunJson.Options)?.Summary ?? string.Empty; }
        catch (JsonException) { return string.Empty; }
    }
}

// ----------------------------------------------------------------- consulta --

/// <summary>El PDF para firma de una definitiva (contracts/api.md §3.4 <c>GET /{runId}/document</c>).</summary>
public sealed record GetSettlementDocumentQuery(Guid RunPublicId) : IRequest<Result<SettlementDocumentFileDto>>;

public sealed record SettlementDocumentFileDto(string FileName, string ContentType, byte[] Content, bool IsDraft);

public sealed class GetSettlementDocumentQueryValidator : AbstractValidator<GetSettlementDocumentQuery>
{
    public GetSettlementDocumentQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetSettlementDocumentQueryHandler(IApplicationDbContext db, ICurrentTenantService tenant, IDateTimeService clock, ISettlementDocumentRenderer renderer)
    : IRequestHandler<GetSettlementDocumentQuery, Result<SettlementDocumentFileDto>>
{
    public async Task<Result<SettlementDocumentFileDto>> Handle(GetSettlementDocumentQuery request, CancellationToken ct)
    {
        var modelo = await new SettlementDocumentModelBuilder(db, tenant, clock).BuildAsync(request.RunPublicId, ct);
        if (modelo.IsFailure) return Result.Failure<SettlementDocumentFileDto>(modelo.Error);
        var m = modelo.Value;
        var nombre = $"liquidacion-definitiva-{m.EmployeeDocument}-{m.TerminationDate:yyyyMMdd}{(m.IsDraft ? "-borrador" : string.Empty)}.pdf";
        return Result.Success(new SettlementDocumentFileDto(nombre, "application/pdf", renderer.Render(m), m.IsDraft));
    }
}
