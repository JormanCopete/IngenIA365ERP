using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Pila;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.WithholdingRates;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Reports;

// Vistas del centro de reportes de la feature 010, N2 (contracts/api.md §11): pila-lineas, pila-cuadre, pila-inconsistencias y retencion-p2.

/// <summary>Una fila por registro tipo 2 con los campos legibles.</summary>
public sealed record PilaLineasReportQuery(Guid GenerationPublicId, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class PilaLineasReportQueryValidator : AbstractValidator<PilaLineasReportQuery>
{
    public PilaLineasReportQueryValidator() => RuleFor(x => x.GenerationPublicId).NotEmpty();
}

public sealed class PilaLineasReportQueryHandler(ISender sender, ICurrentTenantService tenant, ICurrentUserService user, IDateTimeService clock, PayrollAuditEmitter audit)
    : IRequestHandler<PilaLineasReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(PilaLineasReportQuery request, CancellationToken ct)
    {
        var d = await sender.Send(new GetPilaGenerationQuery(request.GenerationPublicId), ct);
        if (d.IsFailure) return Result.Failure<TablaExportable>(d.Error);
        var g = d.Value.Summary;
        var columnas = new List<ColumnaExportable>
        {
            new("#", TipoDeColumna.Entero), new("Cotizante"), new("Documento"), new("Tipo"), new("Novedades"),
            new("Días", TipoDeColumna.Entero), new("Salario", TipoDeColumna.Moneda), new("IBC", TipoDeColumna.Moneda),
            new("Pensión", TipoDeColumna.Moneda), new("FSP", TipoDeColumna.Moneda), new("Salud", TipoDeColumna.Moneda), new("ARL", TipoDeColumna.Moneda),
            new("CCF", TipoDeColumna.Moneda), new("SENA", TipoDeColumna.Moneda), new("ICBF", TipoDeColumna.Moneda), new("Total", TipoDeColumna.Moneda), new("Exonerado"),
        };
        var filas = d.Value.Lines.Select(l => new FilaExportable([l.LineNumber, l.EmployeeName, l.Document, $"{l.ContributorType}/{l.SubType}", string.Join(" ", l.Novelties),
            l.DaysPension, l.Salary, l.IbcCcf, l.Pension, l.Fsp, l.Health, l.Arl, l.Ccf, l.Sena, l.Icbf, l.Total, l.Exempt ? "S" : "N"])).ToList();
        var t = g.Totals;
        var totales = new FilaExportable([null, $"{g.Contributors} cotizante(s), {g.Lines} línea(s)", null, null, null, null, null, null, t.Pension, t.Fsp, t.Health, t.Arl, t.Ccf, t.Sena, t.Icbf, t.Total, null], Resaltada: true);
        var tabla = new TablaExportable($"PILA {g.Period} · versión {g.Version}",
            $"{tenant.TenantName ?? "Cooperativa"} · layout {g.LayoutCode} · {g.Status} · generado por {g.GeneratedBy} el {g.GeneratedAt:dd/MM/yyyy HH:mm} · informe de {user.UserName ?? "sistema"} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC",
            columnas, filas, totales, [$"Archivo {g.FileName ?? "(sin archivo)"} · cuadre {(g.Balanced ? "balanceado" : "con diferencia")} · fecha límite propuesta {(g.DueDate is { } f ? f.ToString("dd/MM/yyyy") : "—")}"]);
        if (request.Exportacion) await audit.EmitAsync(AuditEventTypes.PayrollRunExported, nameof(PilaGeneration), g.GenerationPublicId, null, new { report = "pila-lineas", lines = g.Lines }, ct);
        return Result.Success(tabla);
    }
}

/// <summary>Totales del archivo contra los aportes que la nómina liquidó, por subsistema (FR-027).</summary>
public sealed record PilaCuadreReportQuery(Guid GenerationPublicId, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class PilaCuadreReportQueryValidator : AbstractValidator<PilaCuadreReportQuery>
{
    public PilaCuadreReportQueryValidator() => RuleFor(x => x.GenerationPublicId).NotEmpty();
}

public sealed class PilaCuadreReportQueryHandler(ISender sender, ICurrentTenantService tenant, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<PilaCuadreReportQuery, Result<TablaExportable>>
{
    private static readonly Dictionary<string, string> Nombres = new()
    {
        ["Pension"] = "Pensión", ["Health"] = "Salud", ["Fsp"] = "Fondo de solidaridad pensional", ["Arl"] = "Riesgos laborales", ["Ccf"] = "Caja de compensación", ["Sena"] = "SENA", ["Icbf"] = "ICBF",
    };

    public async Task<Result<TablaExportable>> Handle(PilaCuadreReportQuery request, CancellationToken ct)
    {
        var d = await sender.Send(new GetPilaGenerationQuery(request.GenerationPublicId), ct);
        if (d.IsFailure) return Result.Failure<TablaExportable>(d.Error);
        var g = d.Value.Summary;
        var columnas = new List<ColumnaExportable> { new("Subsistema"), new("Archivo", TipoDeColumna.Moneda), new("Nómina (comprobantes NM)", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda) };
        var filas = d.Value.Reconciliation.BySubsystem.Select(r => new FilaExportable([Nombres.GetValueOrDefault(r.Subsystem, r.Subsystem), r.FileTotal, r.LedgerTotal, r.Difference], Resaltada: r.Difference != 0m)).ToList();
        var totales = new FilaExportable(["Total", d.Value.Reconciliation.BySubsystem.Sum(r => r.FileTotal), d.Value.Reconciliation.BySubsystem.Sum(r => r.LedgerTotal), d.Value.Reconciliation.BySubsystem.Sum(r => r.Difference)], Resaltada: true);
        var notas = new List<string> { d.Value.Reconciliation.Balanced ? "Cuadre balanceado." : d.Value.Reconciliation.Note ?? "Hay diferencia." };
        notas.AddRange(d.Value.Sources.Select(s => $"Fuente: {s.Label} ({s.Kind} v{s.Version})"));
        var tabla = new TablaExportable($"Cuadre PILA {g.Period} · versión {g.Version}",
            $"{tenant.TenantName ?? "Cooperativa"} · generado por {user.UserName ?? "sistema"} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC", columnas, filas, totales, notas);
        if (request.Exportacion) await audit.EmitAsync(AuditEventTypes.PayrollRunExported, nameof(PilaGeneration), g.GenerationPublicId, null, new { report = "pila-cuadre", balanced = g.Balanced }, ct);
        return Result.Success(tabla);
    }
}

/// <summary>Las inconsistencias de una generación, o de la vigente del período.</summary>
public sealed record PilaInconsistenciasReportQuery(Guid? GenerationPublicId, short? Year, byte? Month, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class PilaInconsistenciasReportQueryValidator : AbstractValidator<PilaInconsistenciasReportQuery>
{
    public PilaInconsistenciasReportQueryValidator() => RuleFor(x => x).Must(x => x.GenerationPublicId is not null || (x.Year is not null && x.Month is not null))
        .WithMessage("Indique la generación, o el año y el mes.");
}

public sealed class PilaInconsistenciasReportQueryHandler(IApplicationDbContext db, ISender sender, ICurrentTenantService tenant, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<PilaInconsistenciasReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(PilaInconsistenciasReportQuery request, CancellationToken ct)
    {
        var id = request.GenerationPublicId;
        if (id is null)
            id = await db.PilaGenerations.AsNoTracking().Where(g => g.Year == request.Year && g.Month == request.Month && g.Status != PilaGenerationStatus.Superseded)
                .OrderByDescending(g => g.Version).Select(g => (Guid?)g.PublicId).FirstOrDefaultAsync(ct);
        if (id is null) return Result.Failure<TablaExportable>(PilaErrors.GenerationNotFound);
        var d = await sender.Send(new GetPilaGenerationQuery(id.Value), ct);
        if (d.IsFailure) return Result.Failure<TablaExportable>(d.Error);
        var g = d.Value.Summary;
        var columnas = new List<ColumnaExportable> { new("Severidad"), new("Código"), new("Campo", TipoDeColumna.Entero), new("Empleado"), new("Mensaje"), new("Dónde se corrige") };
        var filas = d.Value.Issues.Select(i => new FilaExportable([i.Severity == PilaIssueSeverity.Blocking ? "Bloqueante" : "Alerta", i.Code, i.Field, i.EmployeeName, i.Message, i.Link], Resaltada: i.Severity == PilaIssueSeverity.Blocking)).ToList();
        var totales = new FilaExportable([$"{g.Blocking} bloqueante(s), {g.Warnings} alerta(s)", null, null, null, null, null], Resaltada: true);
        return Result.Success(new TablaExportable($"Inconsistencias PILA {g.Period} · versión {g.Version}",
            $"{tenant.TenantName ?? "Cooperativa"} · {user.UserName ?? "sistema"} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC", columnas, filas, totales, []));
    }
}

/// <summary>Mes a mes, promedio, retención teórica y porcentaje de un cálculo, o de todos los de un semestre.</summary>
public sealed record RetencionP2ReportQuery(Guid? CalculationPublicId, short? Year, byte? Semester, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class RetencionP2ReportQueryValidator : AbstractValidator<RetencionP2ReportQuery>
{
    public RetencionP2ReportQueryValidator() => RuleFor(x => x).Must(x => x.CalculationPublicId is not null || (x.Year is not null && x.Semester is not null))
        .WithMessage("Indique el cálculo, o el año y el semestre.");
}

public sealed class RetencionP2ReportQueryHandler(ISender sender, ICurrentTenantService tenant, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<RetencionP2ReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(RetencionP2ReportQuery request, CancellationToken ct)
    {
        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Semestre"), new("Versión", TipoDeColumna.Entero), new("Estado"), new("Mes"),
            new("Ingreso gravable", TipoDeColumna.Moneda), new("Aportes obligatorios", TipoDeColumna.Moneda),
            new("Base promedio", TipoDeColumna.Moneda), new("En UVT", TipoDeColumna.Decimal), new("Retención teórica", TipoDeColumna.Moneda), new("Porcentaje", TipoDeColumna.Decimal),
        };
        var filas = new List<FilaExportable>();
        var notas = new List<string>();
        string titulo;
        if (request.CalculationPublicId is { } id)
        {
            var d = await sender.Send(new GetWithholdingRateCalculationQuery(id), ct);
            if (d.IsFailure) return Result.Failure<TablaExportable>(d.Error);
            var s = d.Value.Summary;
            foreach (var m in d.Value.Months)
                filas.Add(new FilaExportable([s.EmployeeName, s.Document, $"{s.Year}-{s.Semester}", s.Version, s.Status.ToString(), $"{m.Year}-{m.Month:00}{(m.IncludedSpecialRuns ? " (con especial)" : string.Empty)}", m.GrossTaxable, m.MandatoryContributions, null, null, null, null]));
            filas.Add(new FilaExportable([s.EmployeeName, s.Document, $"{s.Year}-{s.Semester}", s.Version, s.Status.ToString(), $"Sumatoria ÷ {d.Value.Divisor:0.##} ({d.Value.DivisorSource}), {d.Value.Sequence}", d.Value.TotalGrossIncome, d.Value.TotalMandatoryContributions, d.Value.AverageBase, d.Value.AverageBaseUvt, d.Value.TheoreticalWithholding, d.Value.Percentage], Resaltada: true));
            notas.Add($"Tabla {d.Value.Table.Code} vigente desde {d.Value.Table.ValidFrom:dd/MM/yyyy}: {d.Value.RangeText}. UVT {d.Value.Uvt:N0}. Base depurada {d.Value.DepuratedBase:N0}; deducciones declaradas {d.Value.TotalDeclaredDeductions:N0}; renta exenta {d.Value.TotalExemptIncome:N0}.");
            notas.AddRange(d.Value.DepurationSteps.Select(p => $"Depuración: {p.Label}{(p.Value is { } v ? $" = {v:N0}" : string.Empty)}"));
            titulo = $"Retención procedimiento 2 · {s.EmployeeName} · {s.Year}-{s.Semester}";
            if (request.Exportacion) await audit.EmitAsync(AuditEventTypes.PayrollRunExported, nameof(WithholdingRateCalculation), s.CalculationPublicId, null, new { report = "retencion-p2" }, ct);
        }
        else
        {
            var lista = await sender.Send(new ListWithholdingRateCalculationsQuery(request.Year, request.Semester), ct);
            if (lista.IsFailure) return Result.Failure<TablaExportable>(lista.Error);
            foreach (var s in lista.Value.Where(x => x.Status != WithholdingRateCalculationStatus.Superseded))
                filas.Add(new FilaExportable([s.EmployeeName, s.Document, $"{s.Year}-{s.Semester}", s.Version, s.Status.ToString(), $"{s.MonthsUsed} mes(es) ÷ {s.Divisor:0.##}", null, null, s.AverageBase, s.AverageBaseUvt, s.TheoreticalWithholding, s.Percentage]));
            titulo = $"Retención procedimiento 2 · {request.Year}-{request.Semester}";
        }
        return Result.Success(new TablaExportable(titulo, $"{tenant.TenantName ?? "Cooperativa"} · {user.UserName ?? "sistema"} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC", columnas, filas, null, notas));
    }
}
