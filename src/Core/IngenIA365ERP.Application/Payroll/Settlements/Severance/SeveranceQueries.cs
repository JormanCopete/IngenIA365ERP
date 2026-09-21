using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

// ------------------------------------------------------------------- lista --

/// <summary>Las liquidaciones de cesantías e intereses (contracts/api.md §3.2 <c>GET /?year=&amp;status=</c>), de la más reciente a la más vieja, con total de cesantías, de intereses y el estado de la consignación por fondo.</summary>
public sealed record ListSeveranceRunsQuery(int? Year = null, string? Status = null) : IRequest<Result<IReadOnlyList<SeveranceRunListItemDto>>>;

public sealed class ListSeveranceRunsQueryValidator : AbstractValidator<ListSeveranceRunsQuery>
{
    public ListSeveranceRunsQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).When(x => x.Year is not null);
        RuleFor(x => x.Status).Must(s => s is null || Enum.TryParse<PayrollRunStatus>(s, true, out _))
            .WithMessage("El estado debe ser Draft, Stale, Superseded, Approved o Reversed.");
    }
}

public sealed class ListSeveranceRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListSeveranceRunsQuery, Result<IReadOnlyList<SeveranceRunListItemDto>>>
{
    public async Task<Result<IReadOnlyList<SeveranceRunListItemDto>>> Handle(ListSeveranceRunsQuery request, CancellationToken ct)
    {
        var query = db.PayrollRuns.AsNoTracking().Where(r => r.Kind == PayrollRunKind.Severance);
        if (request.Year is { } anio) query = query.Where(r => r.Year == anio);
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<PayrollRunStatus>(request.Status, true, out var estado))
            query = query.Where(r => r.Status == estado);
        var corridas = await query.OrderByDescending(r => r.Year).ThenByDescending(r => r.CutoffDate).ThenByDescending(r => r.Version).ToListAsync(ct);
        if (corridas.Count == 0) return Result.Success<IReadOnlyList<SeveranceRunListItemDto>>([]);

        var idsCorridas = corridas.Select(r => r.Id).ToList();
        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            where idsCorridas.Contains(re.PayrollRunId)
            select new { re.Id, re.PayrollRunId, e.SeveranceFundId }).ToListAsync(ct);
        var idsFilas = filas.Select(f => f.Id).ToList();
        var lineas = await db.PayrollRunLines.AsNoTracking()
            .Where(l => idsFilas.Contains(l.PayrollRunEmployeeId)
                        && (l.ConceptCode == WellKnownConceptCodes.Severance || l.ConceptCode == WellKnownConceptCodes.SeveranceInterest))
            .Select(l => new { l.PayrollRunEmployeeId, l.ConceptCode, l.Amount })
            .ToListAsync(ct);
        var pagados = await db.PayrollPayments.AsNoTracking()
            .Where(p => idsFilas.Contains(p.PayrollRunEmployeeId) && !p.IsReverted)
            .Select(p => p.PayrollRunEmployeeId).Distinct().ToListAsync(ct);
        var consignaciones = await db.SeveranceFundDeposits.AsNoTracking().Where(d => idsCorridas.Contains(d.PayrollRunId)).ToListAsync(ct);
        var idsFondos = filas.Select(f => f.SeveranceFundId).Where(id => id > 0).Distinct().ToList();
        var fondos = idsFondos.Count == 0
            ? new Dictionary<int, (Guid PublicId, string Name)>()
            : await db.SeveranceProviders.AsNoTracking().IgnoreQueryFilters().Where(f => idsFondos.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => (f.PublicId, f.Name), ct);
        var idsDocumentos = corridas.Where(r => r.AccountingDocumentId != null).Select(r => r.AccountingDocumentId!.Value).ToList();
        var documentos = idsDocumentos.Count == 0
            ? new Dictionary<long, (Guid PublicId, string Numero)>()
            : (await db.AccountingDocuments.AsNoTracking().Where(d => idsDocumentos.Contains(d.Id))
                .Select(d => new { d.Id, d.PublicId, Tipo = d.VoucherType!.Code, d.Number })
                .ToListAsync(ct)).ToDictionary(d => d.Id, d => (PublicId: d.PublicId, Numero: $"{d.Tipo}-{d.Number}"));

        var lista = new List<SeveranceRunListItemDto>(corridas.Count);
        foreach (var run in corridas)
        {
            var filasDeLaCorrida = filas.Where(f => f.PayrollRunId == run.Id).ToList();
            var idsDeLaCorrida = filasDeLaCorrida.Select(f => f.Id).ToHashSet();
            var lineasDeLaCorrida = lineas.Where(l => idsDeLaCorrida.Contains(l.PayrollRunEmployeeId)).ToList();
            var porFila = lineasDeLaCorrida.Where(l => l.ConceptCode == WellKnownConceptCodes.Severance).ToLookup(l => l.PayrollRunEmployeeId);

            var bloques = filasDeLaCorrida.GroupBy(f => f.SeveranceFundId)
                .Select(g =>
                {
                    fondos.TryGetValue(g.Key, out var fondo);
                    var consignacion = g.Key > 0 ? consignaciones.FirstOrDefault(c => c.PayrollRunId == run.Id && c.SeveranceFundId == g.Key) : null;
                    var conCesantias = g.Where(f => porFila[f.Id].Any(l => l.Amount != 0m)).ToList();
                    return new SeveranceFundSummaryDto(g.Key > 0 ? fondo.PublicId : null, g.Key > 0 ? fondo.Name : DepositScheduleBuilder.SinFondo,
                        conCesantias.Count, conCesantias.Sum(f => porFila[f.Id].Sum(l => l.Amount)),
                        consignacion?.DepositedAt, consignacion?.DepositedBy, consignacion?.Reference);
                })
                .Where(b => b.Employees > 0)
                .OrderBy(b => b.FundPublicId is null ? 1 : 0).ThenBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            documentos.TryGetValue(run.AccountingDocumentId ?? -1, out var documento);
            lista.Add(new SeveranceRunListItemDto(
                run.PublicId, run.Year ?? run.CutoffDate!.Value.Year, run.CutoffDate!.Value, run.PayDate, run.Version, run.Status.ToString(),
                run.EmployeeCount, run.TotalNet,
                lineasDeLaCorrida.Where(l => l.ConceptCode == WellKnownConceptCodes.Severance).Sum(l => l.Amount),
                lineasDeLaCorrida.Where(l => l.ConceptCode == WellKnownConceptCodes.SeveranceInterest).Sum(l => l.Amount),
                run.CalculatedAt, run.CalculatedBy, run.ApprovedAt, run.ApprovedBy,
                idsDeLaCorrida.Count(id => pagados.Contains(id)),
                run.AccountingDocumentId is null ? null : documento.PublicId,
                run.AccountingDocumentId is null ? null : documento.Numero,
                bloques));
        }
        return Result.Success<IReadOnlyList<SeveranceRunListItemDto>>(lista);
    }
}

// --------------------------------------------------- relación de consignación --

/// <summary>La relación de consignación por fondo (contracts/api.md §3.2 <c>GET /{runId}/deposit-schedule</c>); sirve sobre el borrador (para revisarla) y sobre la aprobada (para entregarla).</summary>
public sealed record GetDepositScheduleQuery(Guid RunPublicId) : IRequest<Result<DepositScheduleDto>>;

public sealed class GetDepositScheduleQueryValidator : AbstractValidator<GetDepositScheduleQuery>
{
    public GetDepositScheduleQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetDepositScheduleQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDepositScheduleQuery, Result<DepositScheduleDto>>
{
    public async Task<Result<DepositScheduleDto>> Handle(GetDepositScheduleQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<DepositScheduleDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Severance) return Result.Failure<DepositScheduleDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Severance));
        return Result.Success(await new DepositScheduleBuilder(db).BuildAsync(run, ct));
    }
}

// ------------------------------------------------------- archivo plano del fondo --

/// <summary>
/// El archivo plano para el portal del fondo (contracts/archivos.md §3.2). El motor de formatos
/// parametrizables llega en N4 (US8, <c>scope = SeveranceDeposit</c>); hasta entonces ningún fondo
/// tiene formato vigente y la ruta responde 422 <c>Payroll.Severance.FundFormatMissing</c>: la
/// relación de §3.1 sigue siendo la entrega válida.
/// </summary>
public sealed record GetFundDepositFileQuery(Guid RunPublicId, Guid FundPublicId, Guid? FormatPublicId = null) : IRequest<Result<FundDepositFileDto>>;

public sealed record FundDepositFileDto(string FileName, string ContentType, byte[] Content);

public sealed class GetFundDepositFileQueryValidator : AbstractValidator<GetFundDepositFileQuery>
{
    public GetFundDepositFileQueryValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.FundPublicId).NotEmpty();
    }
}

public sealed class GetFundDepositFileQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFundDepositFileQuery, Result<FundDepositFileDto>>
{
    public async Task<Result<FundDepositFileDto>> Handle(GetFundDepositFileQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<FundDepositFileDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Severance) return Result.Failure<FundDepositFileDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Severance));
        if (!await db.SeveranceProviders.AsNoTracking().AnyAsync(f => f.PublicId == request.FundPublicId, ct))
            return Result.Failure<FundDepositFileDto>(SeveranceErrors.FundNotFound);
        // N4 (T139): aquí entra FlatFileWriter con el formato vigente del fondo (scope = SeveranceDeposit).
        return Result.Failure<FundDepositFileDto>(SettlementErrors.SeveranceFundFormatMissing);
    }
}
