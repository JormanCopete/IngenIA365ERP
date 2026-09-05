using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Novelties.Queries;

public sealed record NoveltyDto(
    Guid PublicId,
    Guid PeriodPublicId,
    Guid EmployeePublicId,
    string EmployeeName,
    string EmployeeDocument,
    string ConceptCode,
    string ConceptName,
    string Nature,
    decimal? Quantity,
    decimal? Amount,
    DateTime? StartDate,
    DateTime? EndDate,
    int DaysInPeriod,
    int CarryOverDays,
    decimal? EstimatedAmount,
    string Status,
    string? StatusReason,
    string Origin,
    int? InstallmentNumber,
    int? InstallmentTotal,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    Guid? SupersedesPublicId,
    string? Notes);

// ------------------------------------------------------------------- listar --

/// <summary>FR-006: novedades del período con filtros y el valor previsto de cada una.</summary>
public sealed record ListNoveltiesQuery(
    Guid PeriodPublicId,
    Guid? EmployeeId = null,
    string? ConceptCode = null,
    string? Status = null,
    string? Origin = null,
    string? Search = null) : IRequest<Result<IReadOnlyList<NoveltyDto>>>;

public sealed class ListNoveltiesQueryHandler(
    IApplicationDbContext db,
    CalculationInputLoader loader,
    ILogger<ListNoveltiesQueryHandler> logger)
    : IRequestHandler<ListNoveltiesQuery, Result<IReadOnlyList<NoveltyDto>>>
{
    public async Task<Result<IReadOnlyList<NoveltyDto>>> Handle(ListNoveltiesQuery request, CancellationToken ct)
    {
        var period = await db.PayPeriods.AsNoTracking().Include(p => p.PayrollPlan)
            .FirstOrDefaultAsync(p => p.PublicId == request.PeriodPublicId, ct);
        if (period is null)
            return Result.Failure<IReadOnlyList<NoveltyDto>>(new Error("Payroll.PeriodNotFound", "No existe el período de pago indicado."));

        var query =
            from n in db.PayrollNovelties.AsNoTracking()
            join e in db.Employees.AsNoTracking() on n.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            join c in db.PayrollConceptDefinitions.AsNoTracking() on n.ConceptDefinitionId equals c.Id
            where n.PayPeriodId == period.Id
            select new { n, e, p, c };

        if (request.EmployeeId is { } emp) query = query.Where(x => x.e.PublicId == emp);
        if (!string.IsNullOrWhiteSpace(request.ConceptCode)) { var code = request.ConceptCode.Trim().ToUpperInvariant(); query = query.Where(x => x.n.ConceptCode == code); }
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<NoveltyStatus>(request.Status, true, out var st)) query = query.Where(x => x.n.Status == st);
        if (!string.IsNullOrWhiteSpace(request.Origin) && Enum.TryParse<NoveltyOrigin>(request.Origin, true, out var or)) query = query.Where(x => x.n.Origin == or);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x => x.p.FirstName.Contains(s) || x.p.LastName.Contains(s) || x.p.TaxId.Contains(s)
                                     || x.c.Name.Contains(s) || x.n.ConceptCode.Contains(s));
        }

        var filas = await query
            .OrderBy(x => x.p.LastName).ThenBy(x => x.p.FirstName).ThenBy(x => x.n.Id)
            .ToListAsync(ct);

        var supersedes = filas.Where(x => x.n.SupersedesNoveltyId != null).Select(x => x.n.SupersedesNoveltyId!.Value).Distinct().ToList();
        var publicIdsDeAnteriores = supersedes.Count == 0
            ? new Dictionary<int, Guid>()
            : await db.PayrollNovelties.AsNoTracking().Where(n => supersedes.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.PublicId, ct);

        var estimados = await EstimarAsync(period, ct);

        var dtos = filas.Select(x => new NoveltyDto(
            x.n.PublicId, period.PublicId, x.e.PublicId, $"{x.p.FirstName} {x.p.LastName}".Trim(), x.p.TaxId,
            x.n.ConceptCode, x.c.Name, x.c.Nature.ToString(),
            x.n.Quantity, x.n.Amount, x.n.StartDate, x.n.EndDate, x.n.DaysInPeriod, x.n.CarryOverDays,
            x.n.Status == NoveltyStatus.Active ? estimados.GetValueOrDefault(x.n.PublicId) : null,
            x.n.Status.ToString(), x.n.StatusReason, x.n.Origin.ToString(),
            x.n.InstallmentNumber, x.n.InstallmentTotal,
            x.n.CreatedAt, x.n.CreatedBy, x.n.UpdatedAt, x.n.UpdatedBy,
            x.n.SupersedesNoveltyId is { } sid ? publicIdsDeAnteriores.GetValueOrDefault(sid) : null,
            x.n.Notes)).ToList();

        return Result.Success<IReadOnlyList<NoveltyDto>>(dtos);
    }

    /// <summary>
    /// Valor previsto por novedad: si el período está aprobado, el valor real de la corrida
    /// aprobada; si no, el que daría el motor hoy con el salario vigente. Si falta un
    /// parámetro legal, no hay estimación (la lista no se cae por eso; el cálculo sí se
    /// negará y lo dirá).
    /// </summary>
    private async Task<Dictionary<Guid, decimal?>> EstimarAsync(Domain.Entities.Payroll.PayPeriod period, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, decimal?>();
        if (period.Status is PayPeriodStatus.Approved or PayPeriodStatus.Reversed && period.RunPublicId is { } runId)
        {
            var lineas = await (
                from l in db.PayrollRunLines.AsNoTracking()
                join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
                join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
                join n in db.PayrollNovelties.AsNoTracking() on l.NoveltyId equals n.Id
                where r.PublicId == runId
                select new { n.PublicId, l.Amount }).ToListAsync(ct);
            foreach (var g in lineas.GroupBy(x => x.PublicId)) resultado[g.Key] = g.Sum(x => x.Amount);
            return resultado;
        }

        try
        {
            var batch = await loader.LoadAsync(period, ct);
            if (batch.MissingRequiredParameters.Count > 0) return resultado;
            var engine = new PayrollCalculationEngine();
            foreach (var e in batch.Employees.Where(e => e.Novelties.Count > 0))
            {
                var r = engine.Calculate(batch.InputFor(e));
                foreach (var g in r.Lines.Where(l => l.NoveltyPublicId is not null).GroupBy(l => l.NoveltyPublicId!.Value))
                    resultado[g.Key] = g.Sum(l => l.Amount);
            }
        }
        catch (CalculationRefusedException ex)
        {
            logger.LogInformation("Sin valor estimado para las novedades del período {Period}: {Reason}", period.PublicId, ex.Message);
        }
        return resultado;
    }
}

// ---------------------------------------------------------------- historial --

/// <summary>Cadena de versiones de una novedad: la original, sus correcciones y la anulación, con quién y cuándo (FR-005).</summary>
public sealed record GetNoveltyHistoryQuery(Guid NoveltyPublicId) : IRequest<Result<IReadOnlyList<NoveltyDto>>>;

public sealed class GetNoveltyHistoryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetNoveltyHistoryQuery, Result<IReadOnlyList<NoveltyDto>>>
{
    public async Task<Result<IReadOnlyList<NoveltyDto>>> Handle(GetNoveltyHistoryQuery request, CancellationToken ct)
    {
        var actual = await db.PayrollNovelties.AsNoTracking().FirstOrDefaultAsync(n => n.PublicId == request.NoveltyPublicId, ct);
        if (actual is null)
            return Result.Failure<IReadOnlyList<NoveltyDto>>(new Error("Payroll.NoveltyNotFound", "No existe la novedad indicada."));

        // Hacia atrás por SupersedesNoveltyId y hacia adelante por quien la reemplazó.
        var ids = new HashSet<int> { actual.Id };
        var cursor = actual;
        while (cursor.SupersedesNoveltyId is { } prev)
        {
            cursor = await db.PayrollNovelties.AsNoTracking().FirstAsync(n => n.Id == prev, ct);
            ids.Add(cursor.Id);
        }
        var frente = actual.Id;
        while (true)
        {
            var siguiente = await db.PayrollNovelties.AsNoTracking().FirstOrDefaultAsync(n => n.SupersedesNoveltyId == frente, ct);
            if (siguiente is null) break;
            ids.Add(siguiente.Id);
            frente = siguiente.Id;
        }

        var filas = await (
            from n in db.PayrollNovelties.AsNoTracking()
            join e in db.Employees.AsNoTracking() on n.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            join c in db.PayrollConceptDefinitions.AsNoTracking() on n.ConceptDefinitionId equals c.Id
            join pp in db.PayPeriods.AsNoTracking() on n.PayPeriodId equals pp.Id
            where ids.Contains(n.Id)
            orderby n.Id
            select new { n, e, p, c, pp }).ToListAsync(ct);
        var publicIds = filas.ToDictionary(x => x.n.Id, x => x.n.PublicId);

        var dtos = filas.Select(x => new NoveltyDto(
            x.n.PublicId, x.pp.PublicId, x.e.PublicId, $"{x.p.FirstName} {x.p.LastName}".Trim(), x.p.TaxId,
            x.n.ConceptCode, x.c.Name, x.c.Nature.ToString(),
            x.n.Quantity, x.n.Amount, x.n.StartDate, x.n.EndDate, x.n.DaysInPeriod, x.n.CarryOverDays, null,
            x.n.Status.ToString(), x.n.StatusReason, x.n.Origin.ToString(), x.n.InstallmentNumber, x.n.InstallmentTotal,
            x.n.CreatedAt, x.n.CreatedBy, x.n.UpdatedAt, x.n.UpdatedBy,
            x.n.SupersedesNoveltyId is { } sid ? publicIds.GetValueOrDefault(sid) : null, x.n.Notes)).ToList();

        return Result.Success<IReadOnlyList<NoveltyDto>>(dtos);
    }
}

// ------------------------------------------------------------ salarios --

public sealed record SalaryChangeDto(long Id, DateTime EffectiveDate, decimal NewSalary, string? RegisteredBy, DateTime? RegisteredAt);

public sealed record ListSalaryChangesQuery(Guid EmployeePublicId) : IRequest<Result<IReadOnlyList<SalaryChangeDto>>>;

public sealed class ListSalaryChangesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListSalaryChangesQuery, Result<IReadOnlyList<SalaryChangeDto>>>
{
    public async Task<Result<IReadOnlyList<SalaryChangeDto>>> Handle(ListSalaryChangesQuery request, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId, ct);
        if (employee is null)
            return Result.Failure<IReadOnlyList<SalaryChangeDto>>(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));

        var cambios = await db.SalaryChanges.AsNoTracking()
            .Where(s => s.EmployeeId == employee.Id)
            .OrderByDescending(s => s.EffectiveDate)
            .Select(s => new SalaryChangeDto(s.Id, s.EffectiveDate, s.NewSalary, s.CreatedBy ?? s.UserName, s.EntryDate ?? s.CreatedAt))
            .ToListAsync(ct);

        if (cambios.Count == 0)
            cambios.Add(new SalaryChangeDto(0, employee.JoinDate, employee.Salary, null, null));

        return Result.Success<IReadOnlyList<SalaryChangeDto>>(cambios);
    }
}
