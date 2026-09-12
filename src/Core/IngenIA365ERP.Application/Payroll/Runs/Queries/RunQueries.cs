using FluentValidation;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs.Queries;

/// <summary>Arma <see cref="RunSummaryDto"/> a partir de la corrida (compartido por las consultas).</summary>
public sealed class RunSummaryBuilder(IApplicationDbContext db)
{
    public async Task<RunSummaryDto> BuildAsync(PayrollRun run, Guid periodPublicId, bool detailed, CancellationToken ct)
    {
        var porConcepto = new List<RunConceptTotalDto>();
        var bloqueos = new List<RunBlockerDto>();
        var cambiados = 0;

        var empleados = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            select new { re.Id, re.Flags, re.ChangedFromPreviousRun, re.NotesJson, e.PublicId, Nombre = p.FirstName + " " + p.LastName }).ToListAsync(ct);
        cambiados = empleados.Count(e => e.ChangedFromPreviousRun);

        if (detailed)
        {
            var ids = empleados.Select(e => e.Id).ToList();
            porConcepto = await db.PayrollRunLines.AsNoTracking()
                .Where(l => ids.Contains(l.PayrollRunEmployeeId))
                .GroupBy(l => new { l.ConceptCode, l.ConceptName, l.Nature })
                .Select(g => new RunConceptTotalDto(g.Key.ConceptCode, g.Key.ConceptName, g.Key.Nature.ToString(),
                    g.Select(x => x.PayrollRunEmployeeId).Distinct().Count(), g.Sum(x => x.Amount)))
                .ToListAsync(ct);
            porConcepto = porConcepto.OrderBy(c => c.Nature).ThenBy(c => c.Code, StringComparer.Ordinal).ToList();

            foreach (var e in empleados.Where(e => e.Flags != RunEmployeeFlag.None))
            {
                var notas = Notas(e.NotesJson);
                foreach (var flag in RunJson.FlagNames(e.Flags))
                    bloqueos.Add(new RunBlockerDto(e.PublicId, e.Nombre.Trim(), flag,
                        notas.Refusals.FirstOrDefault() ?? RunJson.FlagLabel(Enum.Parse<RunEmployeeFlag>(flag))));
            }
        }

        var documento = run.AccountingDocumentId is { } docId
            ? await db.AccountingDocuments.AsNoTracking().Where(d => d.Id == docId).Select(d => new { d.PublicId, d.VoucherTypeCode, d.DocumentNumber }).FirstOrDefaultAsync(ct)
            : null;
        var reverso = run.ReversalAccountingDocumentId is { } revId
            ? await db.AccountingDocuments.AsNoTracking().Where(d => d.Id == revId).Select(d => (Guid?)d.PublicId).FirstOrDefaultAsync(ct)
            : null;

        var excepciones = string.IsNullOrWhiteSpace(run.ExceptionsJson)
            ? []
            : JsonSerializer.Deserialize<List<ApprovalExceptionDto>>(run.ExceptionsJson, RunJson.Options) ?? [];

        return new RunSummaryDto(run.PublicId, periodPublicId, run.Version, run.Status.ToString(),
            run.CalculatedAt, run.CalculatedBy, run.ApprovedAt, run.ApprovedBy, run.ReversedAt, run.ReversedBy, run.ReversalReason,
            run.EmployeeCount,
            new RunTotalsDto(run.TotalEarnings, run.TotalDeductions, run.TotalEmployerContributions, run.TotalProvisions, run.TotalNet, run.RoundingAdjustment),
            porConcepto, bloqueos, cambiados, run.InputsHash,
            documento?.PublicId, documento is null ? null : $"{documento.VoucherTypeCode}-{documento.DocumentNumber}",
            reverso, run.ApprovedWithoutSegregation, excepciones, run.DiscardedAt, run.DiscardedBy, run.DiscardReason);
    }

    public static RunEmployeeNotes Notas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new RunEmployeeNotes([], []);
        try { return JsonSerializer.Deserialize<RunEmployeeNotes>(json, RunJson.Options) ?? new RunEmployeeNotes([], []); }
        catch (JsonException) { return new RunEmployeeNotes([], []); }
    }
}

// ------------------------------------------------------------- corrida vigente --

/// <summary>La corrida más reciente del período (borrador, desactualizada, aprobada o reversada); 404 si nunca se calculó.</summary>
public sealed record GetCurrentRunQuery(Guid PeriodPublicId) : IRequest<Result<RunSummaryDto>>;

public sealed class GetCurrentRunQueryHandler(IApplicationDbContext db, RunSummaryBuilder builder)
    : IRequestHandler<GetCurrentRunQuery, Result<RunSummaryDto>>
{
    public async Task<Result<RunSummaryDto>> Handle(GetCurrentRunQuery request, CancellationToken ct)
    {
        var period = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PeriodPublicId, ct);
        if (period is null) return Result.Failure<RunSummaryDto>(new Error("Payroll.PeriodNotFound", "No existe el período de pago indicado."));
        var run = await db.PayrollRuns.AsNoTracking().Where(r => r.PayPeriodId == period.Id).OrderByDescending(r => r.Version).FirstOrDefaultAsync(ct);
        if (run is null) return Result.Failure<RunSummaryDto>(new Error("Payroll.RunNotFound", "El período todavía no se ha calculado."));
        return Result.Success(await builder.BuildAsync(run, period.PublicId, detailed: true, ct));
    }
}

public sealed record ListRunsQuery(Guid PeriodPublicId) : IRequest<Result<IReadOnlyList<RunSummaryDto>>>;

public sealed class ListRunsQueryHandler(IApplicationDbContext db, RunSummaryBuilder builder)
    : IRequestHandler<ListRunsQuery, Result<IReadOnlyList<RunSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<RunSummaryDto>>> Handle(ListRunsQuery request, CancellationToken ct)
    {
        var period = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PeriodPublicId, ct);
        if (period is null) return Result.Failure<IReadOnlyList<RunSummaryDto>>(new Error("Payroll.PeriodNotFound", "No existe el período de pago indicado."));
        var runs = await db.PayrollRuns.AsNoTracking().Where(r => r.PayPeriodId == period.Id).OrderByDescending(r => r.Version).ToListAsync(ct);
        var lista = new List<RunSummaryDto>();
        foreach (var r in runs) lista.Add(await builder.BuildAsync(r, period.PublicId, detailed: false, ct));
        return Result.Success<IReadOnlyList<RunSummaryDto>>(lista);
    }
}

public sealed record GetRunSummaryQuery(Guid RunPublicId) : IRequest<Result<RunSummaryDto>>;

public sealed class GetRunSummaryQueryHandler(IApplicationDbContext db, RunSummaryBuilder builder)
    : IRequestHandler<GetRunSummaryQuery, Result<RunSummaryDto>>
{
    public async Task<Result<RunSummaryDto>> Handle(GetRunSummaryQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<RunSummaryDto>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));
        return Result.Success(await builder.BuildAsync(run, run.PayPeriod!.PublicId, detailed: true, ct));
    }
}

// ------------------------------------------------------------------- empleados --

public sealed record ListRunEmployeesQuery(Guid RunPublicId, string? Flag = null, bool? Changed = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<RunEmployeeRowDto>>>;

public sealed class ListRunEmployeesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListRunEmployeesQuery, Result<IReadOnlyList<RunEmployeeRowDto>>>
{
    public async Task<Result<IReadOnlyList<RunEmployeeRowDto>>> Handle(ListRunEmployeesQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<IReadOnlyList<RunEmployeeRowDto>>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var query =
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            select new { re, e.PublicId, p.FirstName, p.LastName, p.TaxId };

        if (request.Changed is { } cambiado) query = query.Where(x => x.re.ChangedFromPreviousRun == cambiado);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x => x.FirstName.Contains(s) || x.LastName.Contains(s) || x.TaxId.Contains(s));
        }

        var filas = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(request.Flag) && Enum.TryParse<RunEmployeeFlag>(request.Flag, true, out var flag))
            filas = filas.Where(x => flag == RunEmployeeFlag.None ? x.re.Flags == RunEmployeeFlag.None : x.re.Flags.HasFlag(flag)).ToList();

        var dtos = filas.Select(x => new RunEmployeeRowDto(
            x.PublicId, $"{x.FirstName} {x.LastName}".Trim(), x.TaxId, x.re.EmployeeClass.ToString(), x.re.DaysWorked,
            x.re.TotalEarnings, x.re.TotalDeductions, x.re.TotalEmployerContributions, x.re.TotalProvisions, x.re.NetPay,
            RunJson.FlagNames(x.re.Flags), x.re.ChangedFromPreviousRun,
            RunSummaryBuilder.Notas(x.re.NotesJson) is { } n && (n.Refusals.Count > 0 || n.Skips.Count > 0))).ToList();

        return Result.Success<IReadOnlyList<RunEmployeeRowDto>>(dtos);
    }
}

public sealed record GetRunEmployeeDetailQuery(Guid RunPublicId, Guid EmployeePublicId) : IRequest<Result<RunEmployeeDetailDto>>;

public sealed class GetRunEmployeeDetailQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRunEmployeeDetailQuery, Result<RunEmployeeDetailDto>>
{
    public async Task<Result<RunEmployeeDetailDto>> Handle(GetRunEmployeeDetailQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<RunEmployeeDetailDto>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var fila = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id && e.PublicId == request.EmployeePublicId
            select new { re, p.FirstName, p.LastName, p.TaxId }).FirstOrDefaultAsync(ct);
        if (fila is null) return Result.Failure<RunEmployeeDetailDto>(new Error("Payroll.RunEmployeeNotFound", "El empleado no está en esta corrida."));

        var lineas = await db.PayrollRunLines.AsNoTracking()
            .Where(l => l.PayrollRunEmployeeId == fila.re.Id)
            .OrderBy(l => l.Order)
            .ToListAsync(ct);
        var noveltyIds = lineas.Where(l => l.NoveltyId != null).Select(l => l.NoveltyId!.Value).Distinct().ToList();
        var novedades = noveltyIds.Count == 0
            ? new Dictionary<int, Guid>()
            : await db.PayrollNovelties.AsNoTracking().Where(n => noveltyIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.PublicId, ct);

        var tramos = JsonSerializer.Deserialize<List<SalaryTrancheDto>>(fila.re.SalaryTranchesJson, RunJson.Options) ?? [];
        var bases = JsonSerializer.Deserialize<List<ExplanationStep>>(fila.re.BasesJson, RunJson.Options) ?? [];
        var notas = RunSummaryBuilder.Notas(fila.re.NotesJson);

        var dto = new RunEmployeeDetailDto(run.PublicId, request.EmployeePublicId, $"{fila.FirstName} {fila.LastName}".Trim(), fila.TaxId,
            fila.re.EmployeeClass.ToString(), fila.re.DaysWorked, tramos,
            lineas.Select(l => new RunLineDto(l.PublicId, l.ConceptCode, l.ConceptName, l.Nature.ToString(), l.Quantity, l.BaseAmount, l.Factor,
                l.RangeFrom, l.RangeTo, l.Amount, l.AffectsAccounting,
                l.NoveltyId is { } nid ? novedades.GetValueOrDefault(nid) : null, l.Order,
                JsonSerializer.Deserialize<JsonElement>(l.ExplanationJson, RunJson.Options))).ToList(),
            new RunTotalsDto(fila.re.TotalEarnings, fila.re.TotalDeductions, fila.re.TotalEmployerContributions, fila.re.TotalProvisions, fila.re.NetPay,
                lineas.Where(l => l.ConceptCode == WellKnownConceptCodes.RoundingAdjustment).Sum(l => l.Amount)),
            RunJson.FlagNames(fila.re.Flags), fila.re.ChangedFromPreviousRun, bases, notas.Refusals, notas.Skips);

        return Result.Success(dto);
    }
}

// Principio VIII: toda consulta lleva su validador, aunque sea de identidad.
public sealed class GetCurrentRunQueryValidator : AbstractValidator<GetCurrentRunQuery>
{
    public GetCurrentRunQueryValidator() => RuleFor(x => x.PeriodPublicId).NotEmpty();
}

public sealed class ListRunsQueryValidator : AbstractValidator<ListRunsQuery>
{
    public ListRunsQueryValidator() => RuleFor(x => x.PeriodPublicId).NotEmpty();
}

public sealed class GetRunSummaryQueryValidator : AbstractValidator<GetRunSummaryQuery>
{
    public GetRunSummaryQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ListRunEmployeesQueryValidator : AbstractValidator<ListRunEmployeesQuery>
{
    public ListRunEmployeesQueryValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class GetRunEmployeeDetailQueryValidator : AbstractValidator<GetRunEmployeeDetailQuery>
{
    public GetRunEmployeeDetailQueryValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.EmployeePublicId).NotEmpty();
    }
}
