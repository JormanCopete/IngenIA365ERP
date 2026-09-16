using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Novelties.Queries;
using IngenIA365ERP.Application.Payroll.Runs.Queries;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Feature 006 US5: cinco vistas de nómina como <see cref="TablaExportable"/>. Cada una se
/// apoya en las consultas que ya usa Liquidación —mismos números que la pantalla— y sólo
/// las reordena en una tabla que los exportadores saben convertir. Ninguna calcula nada.
/// </summary>
public static class ReportesDeNomina
{
    public static string NombreNaturaleza(string nature) => nature switch
    {
        "Earning" => "Devengos",
        "Deduction" => "Deducciones",
        "EmployerContribution" => "Aportes del empleador",
        "Provision" => "Provisiones",
        "Informative" => "Informativos",
        _ => nature,
    };

    public static string NombreOrigen(string origin, int? cuota, int? total) => origin switch
    {
        "Manual" => "Manual",
        "Import" => "Importada",
        "Recurring" => total is { } t ? $"Recurrente (cuota {cuota}/{t})" : $"Recurrente (cuota {cuota})",
        "Retroactive" => "Ajuste retroactivo",
        "LoanDeduction" => "Cartera",
        "CarryOver" => "Arrastre",
        _ => origin,
    };

    public static string NombreEstadoNovedad(string status) => status switch
    {
        "Active" => "Activa",
        "Superseded" => "Corregida",
        "Cancelled" => "Anulada",
        _ => status,
    };
}

// ------------------------------------------------------------ 1. comprobante --

public sealed record ComprobantePorEmpleadoReportQuery(Guid RunPublicId, Guid EmployeePublicId) : IRequest<Result<TablaExportable>>;

public sealed class ComprobantePorEmpleadoReportQueryHandler(ISender sender) : IRequestHandler<ComprobantePorEmpleadoReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(ComprobantePorEmpleadoReportQuery request, CancellationToken ct)
    {
        var corrida = await sender.Send(new GetRunSummaryQuery(request.RunPublicId), ct);
        if (corrida.IsFailure) return Result.Failure<TablaExportable>(corrida.Error);
        var detalle = await sender.Send(new GetRunEmployeeDetailQuery(request.RunPublicId, request.EmployeePublicId), ct);
        if (detalle.IsFailure) return Result.Failure<TablaExportable>(detalle.Error);
        var d = detalle.Value;

        var columnas = new List<ColumnaExportable>
        {
            new("Concepto"), new("Código"), new("Cantidad", TipoDeColumna.Decimal), new("Base", TipoDeColumna.Moneda), new("Valor", TipoDeColumna.Moneda),
        };
        var filas = d.Lines.OrderBy(l => l.Order).Select(l => new FilaExportable(
            [l.ConceptName, l.ConceptCode, l.Quantity, l.BaseAmount, l.Amount], ReportesDeNomina.NombreNaturaleza(l.Nature))).ToList();
        var totales = new FilaExportable(["Neto a pagar", "", null, null, d.Totals.Net], Resaltada: true);
        var notas = new List<string>
        {
            $"Devengos {d.Totals.Earnings:N0} · Deducciones {d.Totals.Deductions:N0} · Aportes del empleador {d.Totals.EmployerContributions:N0} · Provisiones {d.Totals.Provisions:N0}",
            $"Días liquidados: {d.DaysWorked}. Clase: {d.EmployeeClass}.",
        };
        notas.AddRange(d.Refusals);
        return Result.Success(new TablaExportable(
            $"Comprobante de nómina · {d.EmployeeName} ({d.Document})",
            $"Corrida v{corrida.Value.Version} · {corrida.Value.Status} · calculada {corrida.Value.CalculatedAt:dd/MM/yyyy}",
            columnas, filas, totales, notas));
    }
}

// --------------------------------------------------------- 2. resumen corrida --

public sealed record ResumenCorridaReportQuery(Guid RunPublicId) : IRequest<Result<TablaExportable>>;

public sealed class ResumenCorridaReportQueryHandler(ISender sender) : IRequestHandler<ResumenCorridaReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(ResumenCorridaReportQuery request, CancellationToken ct)
    {
        var r = await sender.Send(new GetRunSummaryQuery(request.RunPublicId), ct);
        if (r.IsFailure) return Result.Failure<TablaExportable>(r.Error);
        var c = r.Value;
        var columnas = new List<ColumnaExportable> { new("Concepto"), new("Código"), new("Empleados", TipoDeColumna.Entero), new("Total", TipoDeColumna.Moneda) };
        var filas = c.ByConcept
            .OrderBy(x => x.Nature switch { "Earning" => 0, "Deduction" => 1, "EmployerContribution" => 2, "Provision" => 3, _ => 4 }).ThenBy(x => x.Code)
            .Select(x => new FilaExportable([x.Name, x.Code, x.Employees, x.Amount], ReportesDeNomina.NombreNaturaleza(x.Nature))).ToList();
        var totales = new FilaExportable(["Neto a pagar", "", c.EmployeeCount, c.Totals.Net], Resaltada: true);
        var notas = new List<string>
        {
            $"Devengos {c.Totals.Earnings:N0} · Deducciones {c.Totals.Deductions:N0} · Aportes del empleador {c.Totals.EmployerContributions:N0} · Provisiones {c.Totals.Provisions:N0} · Ajuste por redondeo {c.Totals.RoundingAdjustment:N0}",
            $"Estado {c.Status}; calculada por {c.CalculatedBy} el {c.CalculatedAt:dd/MM/yyyy HH:mm}" + (c.ApprovedAt is { } a ? $"; aprobada por {c.ApprovedBy} el {a:dd/MM/yyyy HH:mm}" : "") + (c.AccountingDocumentNumber is { } n ? $"; comprobante {n}" : ""),
        };
        if (c.Blockers.Count > 0) notas.Add($"{c.Blockers.Count} bloqueo(s) para aprobar.");
        return Result.Success(new TablaExportable($"Resumen de la corrida v{c.Version}", $"{c.EmployeeCount} empleado(s)", columnas, filas, totales, notas));
    }
}

// -------------------------------------------------- 3. detalle empleado × concepto --

public sealed record DetalleEmpleadoConceptoReportQuery(Guid RunPublicId) : IRequest<Result<TablaExportable>>;

public sealed class DetalleEmpleadoConceptoReportQueryHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<DetalleEmpleadoConceptoReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(DetalleEmpleadoConceptoReportQuery request, CancellationToken ct)
    {
        var resumen = await sender.Send(new GetRunSummaryQuery(request.RunPublicId), ct);
        if (resumen.IsFailure) return Result.Failure<TablaExportable>(resumen.Error);
        var run = await db.PayrollRuns.AsNoTracking().FirstAsync(r => r.PublicId == request.RunPublicId, ct);

        var filasEmpleado = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            orderby p.LastName, p.FirstName
            select new { re.Id, Nombre = (p.FirstName + " " + p.LastName).Trim(), p.TaxId, re.DaysWorked, re.TotalEarnings, re.TotalDeductions, re.TotalEmployerContributions, re.TotalProvisions, re.NetPay }
        ).ToListAsync(ct);
        var ids = filasEmpleado.Select(f => f.Id).ToList();
        var lineas = await db.PayrollRunLines.AsNoTracking()
            .Where(l => ids.Contains(l.PayrollRunEmployeeId))
            .Select(l => new { l.PayrollRunEmployeeId, l.ConceptCode, l.Amount })
            .ToListAsync(ct);

        // Columnas dinámicas: los conceptos de la corrida en el orden del resumen (devengos, deducciones…).
        var conceptos = resumen.Value.ByConcept
            .OrderBy(x => x.Nature switch { "Earning" => 0, "Deduction" => 1, "EmployerContribution" => 2, "Provision" => 3, _ => 4 }).ThenBy(x => x.Code)
            .Select(x => x.Code).ToList();
        var columnas = new List<ColumnaExportable> { new("Empleado"), new("Documento"), new("Días", TipoDeColumna.Entero) };
        columnas.AddRange(conceptos.Select(c => new ColumnaExportable(c, TipoDeColumna.Moneda, c)));
        columnas.AddRange([new("Devengos", TipoDeColumna.Moneda), new("Deducciones", TipoDeColumna.Moneda), new("Neto", TipoDeColumna.Moneda)]);

        var porEmpleado = lineas.GroupBy(l => l.PayrollRunEmployeeId)
            .ToDictionary(g => g.Key, g => g.GroupBy(l => l.ConceptCode).ToDictionary(x => x.Key, x => x.Sum(l => l.Amount)));
        var filas = filasEmpleado.Select(f =>
        {
            var valores = new List<object?> { f.Nombre, f.TaxId, f.DaysWorked };
            porEmpleado.TryGetValue(f.Id, out var montos);
            valores.AddRange(conceptos.Select(c => montos is not null && montos.TryGetValue(c, out var m) ? m : (object?)null));
            valores.AddRange([f.TotalEarnings, f.TotalDeductions, f.NetPay]);
            return new FilaExportable(valores);
        }).ToList();

        var totalesValores = new List<object?> { "Total", "", filasEmpleado.Sum(f => f.DaysWorked) };
        totalesValores.AddRange(conceptos.Select(c => (object?)lineas.Where(l => l.ConceptCode == c).Sum(l => l.Amount)));
        totalesValores.AddRange([filasEmpleado.Sum(f => f.TotalEarnings), filasEmpleado.Sum(f => f.TotalDeductions), filasEmpleado.Sum(f => f.NetPay)]);

        return Result.Success(new TablaExportable($"Detalle por empleado y concepto · corrida v{run.Version}", $"{filasEmpleado.Count} empleado(s), {conceptos.Count} concepto(s)",
            columnas, filas, new FilaExportable(totalesValores, Resaltada: true), []));
    }
}

// ------------------------------------------------------- 4. novedades del período --

public sealed record NovedadesPeriodoReportQuery(Guid PeriodPublicId, bool IncluirAnuladas = true) : IRequest<Result<TablaExportable>>;

public sealed class NovedadesPeriodoReportQueryHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<NovedadesPeriodoReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(NovedadesPeriodoReportQuery request, CancellationToken ct)
    {
        var periodo = await db.PayPeriods.AsNoTracking().Include(p => p.PayrollPlan).FirstOrDefaultAsync(p => p.PublicId == request.PeriodPublicId, ct);
        if (periodo is null) return Result.Failure<TablaExportable>(new Error("Payroll.PeriodNotFound", "No existe el período indicado."));
        var r = await sender.Send(new ListNoveltiesQuery(request.PeriodPublicId), ct);
        if (r.IsFailure) return Result.Failure<TablaExportable>(r.Error);
        var novedades = request.IncluirAnuladas ? r.Value : r.Value.Where(n => n.Status == "Active").ToList();

        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Concepto"), new("Código"), new("Cantidad", TipoDeColumna.Decimal), new("Valor", TipoDeColumna.Moneda),
            new("Desde", TipoDeColumna.Fecha), new("Hasta", TipoDeColumna.Fecha), new("Valor previsto", TipoDeColumna.Moneda), new("Origen"), new("Estado"), new("Registró"), new("Fecha", TipoDeColumna.Fecha),
        };
        var filas = novedades.OrderBy(n => n.EmployeeName).ThenBy(n => n.ConceptCode).Select(n => new FilaExportable(
        [
            n.EmployeeName, n.EmployeeDocument, n.ConceptName, n.ConceptCode, n.Quantity, n.Amount, n.StartDate, n.EndDate, n.EstimatedAmount,
            ReportesDeNomina.NombreOrigen(n.Origin, n.InstallmentNumber, n.InstallmentTotal),
            ReportesDeNomina.NombreEstadoNovedad(n.Status) + (string.IsNullOrWhiteSpace(n.StatusReason) ? "" : $" — {n.StatusReason}"),
            n.CreatedBy, n.CreatedAt,
        ], ReportesDeNomina.NombreNaturaleza(n.Nature))).ToList();

        var subtitulo = $"{periodo.PayrollPlan?.Name} · {periodo.StartDate:dd/MM/yyyy} – {periodo.EndDate:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(periodo.Description) ? "" : $" · {periodo.Description}");
        return Result.Success(new TablaExportable("Novedades del período", subtitulo, columnas, filas, null,
            [$"{novedades.Count} novedad(es); {novedades.Count(n => n.Status == "Active")} activa(s)."]));
    }
}

// ------------------------------------------------------ 5. histórico por empleado --

public sealed record HistoricoEmpleadoReportQuery(Guid EmployeePublicId, DateTime Desde, DateTime Hasta) : IRequest<Result<TablaExportable>>;

public sealed class HistoricoEmpleadoReportQueryHandler(IApplicationDbContext db) : IRequestHandler<HistoricoEmpleadoReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(HistoricoEmpleadoReportQuery request, CancellationToken ct)
    {
        var empleado = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where e.PublicId == request.EmployeePublicId
            select new { e.Id, Nombre = (p.FirstName + " " + p.LastName).Trim(), p.TaxId }).FirstOrDefaultAsync(ct);
        if (empleado is null) return Result.Failure<TablaExportable>(new Error("Employee.NotFound", "Empleado no encontrado."));

        var desde = request.Desde.Date;
        var hasta = request.Hasta.Date;
        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join run in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals run.Id
            join per in db.PayPeriods.AsNoTracking() on run.PayPeriodId equals per.Id
            where re.EmployeeId == empleado.Id && run.Status == PayrollRunStatus.Approved && per.StartDate <= hasta && per.EndDate >= desde
            orderby per.StartDate
            select new { per.Description, per.StartDate, per.EndDate, run.Version, run.ApprovedAt, re.DaysWorked, re.TotalEarnings, re.TotalDeductions, re.TotalEmployerContributions, re.TotalProvisions, re.NetPay }
        ).ToListAsync(ct);

        var columnas = new List<ColumnaExportable>
        {
            new("Período"), new("Desde", TipoDeColumna.Fecha), new("Hasta", TipoDeColumna.Fecha), new("Versión", TipoDeColumna.Entero), new("Aprobada", TipoDeColumna.Fecha), new("Días", TipoDeColumna.Entero),
            new("Devengos", TipoDeColumna.Moneda), new("Deducciones", TipoDeColumna.Moneda), new("Aportes empleador", TipoDeColumna.Moneda), new("Provisiones", TipoDeColumna.Moneda), new("Neto", TipoDeColumna.Moneda),
        };
        var tabla = filas.Select(f => new FilaExportable(
            [f.Description ?? $"{f.StartDate:MMM yyyy}", f.StartDate, f.EndDate, f.Version, f.ApprovedAt, f.DaysWorked, f.TotalEarnings, f.TotalDeductions, f.TotalEmployerContributions, f.TotalProvisions, f.NetPay])).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, filas.Sum(f => f.DaysWorked), filas.Sum(f => f.TotalEarnings), filas.Sum(f => f.TotalDeductions),
            filas.Sum(f => f.TotalEmployerContributions), filas.Sum(f => f.TotalProvisions), filas.Sum(f => f.NetPay)], Resaltada: true);
        return Result.Success(new TablaExportable($"Histórico de nómina · {empleado.Nombre} ({empleado.TaxId})",
            $"Liquidaciones aprobadas entre {desde:dd/MM/yyyy} y {hasta:dd/MM/yyyy}", columnas, tabla, totales,
            [$"{filas.Count} período(s) aprobado(s). Sólo se listan corridas aprobadas; los borradores y las reversadas no."]));
    }
}
