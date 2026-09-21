using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Feature 010 (T045; contracts/api.md §11): las dos vistas del centro de reportes para
/// cualquier liquidación especial —prima, cesantías, vacaciones, definitiva— por <c>runId</c>:
/// <c>liquidacion-especial-resumen</c> (un renglón por empleado: base, días, cada rubro, retención,
/// neto) y <c>liquidacion-especial-detalle</c> (empleado × concepto con su explicación). Son
/// <see cref="TablaExportable"/> como las de la 006; ninguna calcula nada. El permiso es el
/// <c>.View</c> del recurso del <c>Kind</c> de la corrida (la ruta sólo puede exigir uno fijo), y
/// exportar a archivo deja <c>Payroll.Report.Exported</c> en la auditoría.
/// </summary>
public static class ReportesDeLiquidaciones
{
    /// <summary>El recurso de permisos de cada tipo de corrida (contracts/api.md §1); la ordinaria sigue en <c>Payroll.Runs</c>.</summary>
    public static string RecursoDe(PayrollRunKind kind) => kind switch
    {
        PayrollRunKind.ServiceBonus => "Payroll.ServiceBonus",
        PayrollRunKind.Severance => "Payroll.Severance",
        PayrollRunKind.Vacation => "Payroll.Vacations",
        PayrollRunKind.Settlement => "Payroll.Settlements",
        _ => "Payroll.Runs",
    };

    public static string PermisoDeVista(PayrollRunKind kind) => $"{RecursoDe(kind)}.View";

    /// <summary>El rubro principal de cada tipo: su base y sus días encabezan el resumen.</summary>
    public static string RubroPrincipal(PayrollRunKind kind) => kind switch
    {
        PayrollRunKind.ServiceBonus => WellKnownConceptCodes.ServiceBonus,
        PayrollRunKind.Severance => WellKnownConceptCodes.Severance,
        PayrollRunKind.Vacation => WellKnownConceptCodes.VacationPayout,
        PayrollRunKind.Settlement => WellKnownConceptCodes.PendingSalary,
        _ => WellKnownConceptCodes.BasicSalary,
    };

    public static bool EsRetencion(string conceptCode) => conceptCode.StartsWith(WellKnownConceptCodes.Withholding, StringComparison.OrdinalIgnoreCase);

    /// <summary>El resumen de la explicación guardada en la línea (<c>summary</c>), o vacío si no lo trae.</summary>
    public static string ResumenDe(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(explanationJson);
            return doc.RootElement.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException) { return string.Empty; }
    }

    /// <summary>Encabezado obligatorio: cooperativa, liquidación, versión y estado, quién lo pide y cuándo.</summary>
    internal static string Subtitulo(PayrollRun run, string cooperativa, string usuario, DateTime ahora, int empleados) =>
        $"{cooperativa} · {SettlementLabels.Etiqueta(run)} · v{run.Version} · {run.Status} · {empleados} empleado(s) · generado por {usuario} el {ahora:dd/MM/yyyy HH:mm} UTC";

    internal static string Titulo(PayrollRun run) => run.Kind switch
    {
        PayrollRunKind.ServiceBonus => "Prima de servicios",
        PayrollRunKind.Severance => "Cesantías e intereses",
        PayrollRunKind.Vacation => "Liquidación de vacaciones",
        PayrollRunKind.Settlement => "Liquidación definitiva",
        _ => "Liquidación",
    };
}

/// <summary>Lo que las dos vistas comparten: la corrida (con permiso del tipo), sus empleados y sus líneas.</summary>
internal sealed class CargaDeLiquidacionParaReporte(IApplicationDbContext db, IPermissionChecker permissions)
{
    internal sealed record Fila(int RunEmployeeId, Guid EmployeePublicId, string Nombre, string Documento, int DaysWorked, decimal TotalEarnings, decimal TotalDeductions, decimal NetPay, string EmployeeClass);
    internal sealed record Linea(int RunEmployeeId, string ConceptCode, string ConceptName, ConceptNature Nature, decimal? Quantity, decimal? BaseAmount, decimal Amount, int Order, string? ExplanationJson, bool AffectsAccounting);
    internal sealed record Cargado(PayrollRun Run, IReadOnlyList<Fila> Filas, IReadOnlyList<Linea> Lineas);

    public async Task<Result<Cargado>> CargarAsync(Guid runPublicId, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<Cargado>(SettlementErrors.RunNotFound);
        // Sin el .View del tipo la respuesta es la misma que si no existiera (FR-017 de la 002).
        if (!await permissions.HasPermissionAsync(ReportesDeLiquidaciones.PermisoDeVista(run.Kind), ct))
            return Result.Failure<Cargado>(Error.NotFound);

        var filas = (await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            orderby p.LastName, p.FirstName
            select new { re.Id, e.PublicId, Nombre = p.FirstName + " " + p.LastName, p.TaxId, re.DaysWorked, re.TotalEarnings, re.TotalDeductions, re.NetPay, re.EmployeeClass })
            .ToListAsync(ct))
            .Select(x => new Fila(x.Id, x.PublicId, x.Nombre.Trim(), x.TaxId, x.DaysWorked, x.TotalEarnings, x.TotalDeductions, x.NetPay, x.EmployeeClass.ToString()))
            .ToList();
        var ids = filas.Select(f => f.RunEmployeeId).ToList();
        var lineas = await db.PayrollRunLines.AsNoTracking()
            .Where(l => ids.Contains(l.PayrollRunEmployeeId))
            .OrderBy(l => l.Order)
            .Select(l => new Linea(l.PayrollRunEmployeeId, l.ConceptCode, l.ConceptName, l.Nature, l.Quantity, l.BaseAmount, l.Amount, l.Order, l.ExplanationJson, l.AffectsAccounting))
            .ToListAsync(ct);
        return Result.Success(new Cargado(run, filas, lineas));
    }
}

// ------------------------------------------------------------ 1. resumen --

/// <summary><c>liquidacion-especial-resumen</c>: un renglón por empleado. <paramref name="Exportacion"/> es verdadero cuando se pide un archivo y queda en auditoría.</summary>
public sealed record LiquidacionEspecialResumenReportQuery(Guid RunPublicId, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class LiquidacionEspecialResumenReportQueryValidator : AbstractValidator<LiquidacionEspecialResumenReportQuery>
{
    public LiquidacionEspecialResumenReportQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
}

public sealed class LiquidacionEspecialResumenReportQueryHandler(
    IApplicationDbContext db,
    IPermissionChecker permissions,
    ICurrentTenantService tenant,
    ICurrentUserService user,
    IDateTimeService clock,
    PayrollAuditEmitter audit)
    : IRequestHandler<LiquidacionEspecialResumenReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(LiquidacionEspecialResumenReportQuery request, CancellationToken ct)
    {
        var carga = await new CargaDeLiquidacionParaReporte(db, permissions).CargarAsync(request.RunPublicId, ct);
        if (carga.IsFailure) return Result.Failure<TablaExportable>(carga.Error);
        var (run, filas, lineas) = carga.Value;

        var principal = ReportesDeLiquidaciones.RubroPrincipal(run.Kind);
        var rubros = lineas.Where(l => l.Nature == ConceptNature.Earning)
            .GroupBy(l => l.ConceptCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key.Equals(principal, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(g => g.Min(l => l.Order))
            .Select(g => (Code: g.Key, Name: g.First().ConceptName)).ToList();
        var otrasDeducciones = lineas.Any(l => l.Nature == ConceptNature.Deduction && !ReportesDeLiquidaciones.EsRetencion(l.ConceptCode));

        var columnas = new List<ColumnaExportable> { new("Empleado"), new("Documento"), new("Base", TipoDeColumna.Moneda), new("Días", TipoDeColumna.Entero) };
        columnas.AddRange(rubros.Select(r => new ColumnaExportable(r.Name, TipoDeColumna.Moneda, r.Code)));
        columnas.Add(new("Retención", TipoDeColumna.Moneda, "RETENCION"));
        if (otrasDeducciones) columnas.Add(new("Otras deducciones", TipoDeColumna.Moneda, "OTRAS_DEDUCCIONES"));
        columnas.Add(new("Neto", TipoDeColumna.Moneda, "NETO"));

        var porEmpleado = lineas.ToLookup(l => l.RunEmployeeId);
        var tabla = new List<FilaExportable>(filas.Count);
        decimal totalBase = 0m, totalRetencion = 0m, totalOtras = 0m, totalNeto = 0m;
        var totalDias = 0;
        var totalesPorRubro = rubros.ToDictionary(r => r.Code, _ => 0m, StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
        {
            var propias = porEmpleado[f.RunEmployeeId].ToList();
            var lineaPrincipal = propias.FirstOrDefault(l => l.ConceptCode.Equals(principal, StringComparison.OrdinalIgnoreCase))
                                 ?? propias.FirstOrDefault(l => l.Nature == ConceptNature.Earning);
            var baseEmpleado = lineaPrincipal?.BaseAmount ?? 0m;
            var retencion = propias.Where(l => l.Nature == ConceptNature.Deduction && ReportesDeLiquidaciones.EsRetencion(l.ConceptCode)).Sum(l => l.Amount);
            var otras = propias.Where(l => l.Nature == ConceptNature.Deduction && !ReportesDeLiquidaciones.EsRetencion(l.ConceptCode)).Sum(l => l.Amount);

            var valores = new List<object?> { f.Nombre, f.Documento, baseEmpleado, f.DaysWorked };
            foreach (var r in rubros)
            {
                var monto = propias.Where(l => l.ConceptCode.Equals(r.Code, StringComparison.OrdinalIgnoreCase) && l.Nature == ConceptNature.Earning).Sum(l => l.Amount);
                valores.Add(monto);
                totalesPorRubro[r.Code] += monto;
            }
            valores.Add(retencion);
            if (otrasDeducciones) valores.Add(otras);
            valores.Add(f.NetPay);
            tabla.Add(new FilaExportable(valores));

            totalBase += baseEmpleado; totalDias += f.DaysWorked; totalRetencion += retencion; totalOtras += otras; totalNeto += f.NetPay;
        }

        var totales = new List<object?> { "Total", $"{filas.Count} empleado(s)", totalBase, totalDias };
        totales.AddRange(rubros.Select(r => (object?)totalesPorRubro[r.Code]));
        totales.Add(totalRetencion);
        if (otrasDeducciones) totales.Add(totalOtras);
        totales.Add(totalNeto);

        var notas = new List<string>
        {
            $"Corte {run.CutoffDate:dd/MM/yyyy}" + (run.PayDate is { } pd ? $" · fecha de pago {pd:dd/MM/yyyy}" : string.Empty)
                + $" · calculada por {run.CalculatedBy} el {run.CalculatedAt:dd/MM/yyyy HH:mm}"
                + (run.ApprovedAt is { } a ? $" · aprobada por {run.ApprovedBy} el {a:dd/MM/yyyy HH:mm}" : string.Empty)
                + (run.ReversedAt is { } rv ? $" · reversada por {run.ReversedBy} el {rv:dd/MM/yyyy HH:mm}: {run.ReversalReason}" : string.Empty),
            "La base es la del rubro principal de la liquidación (promedio del período que causa la prestación); la retención suma los conceptos RETEFTE_* del empleado.",
        };
        var resultado = new TablaExportable(
            $"{ReportesDeLiquidaciones.Titulo(run)} · resumen por empleado",
            ReportesDeLiquidaciones.Subtitulo(run, tenant.TenantName ?? "Cooperativa", user.UserName ?? "sistema", clock.UtcNow, filas.Count),
            columnas, tabla, new FilaExportable(totales, Resaltada: true), notas);

        if (request.Exportacion)
            await audit.EmitAsync(AuditEventTypes.PayrollRunExported, nameof(PayrollRun), run.PublicId, null,
                new { report = "liquidacion-especial-resumen", kind = run.Kind.ToString(), version = run.Version, employees = filas.Count }, ct);
        return Result.Success(resultado);
    }
}

// ------------------------------------------------------------ 2. detalle --

/// <summary><c>liquidacion-especial-detalle</c>: empleado × concepto con la explicación de cada línea.</summary>
public sealed record LiquidacionEspecialDetalleReportQuery(Guid RunPublicId, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class LiquidacionEspecialDetalleReportQueryValidator : AbstractValidator<LiquidacionEspecialDetalleReportQuery>
{
    public LiquidacionEspecialDetalleReportQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
}

public sealed class LiquidacionEspecialDetalleReportQueryHandler(
    IApplicationDbContext db,
    IPermissionChecker permissions,
    ICurrentTenantService tenant,
    ICurrentUserService user,
    IDateTimeService clock,
    PayrollAuditEmitter audit)
    : IRequestHandler<LiquidacionEspecialDetalleReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(LiquidacionEspecialDetalleReportQuery request, CancellationToken ct)
    {
        var carga = await new CargaDeLiquidacionParaReporte(db, permissions).CargarAsync(request.RunPublicId, ct);
        if (carga.IsFailure) return Result.Failure<TablaExportable>(carga.Error);
        var (run, filas, lineas) = carga.Value;

        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Concepto"), new("Código"), new("Tipo"),
            new("Cantidad", TipoDeColumna.Decimal), new("Base", TipoDeColumna.Moneda), new("Valor", TipoDeColumna.Moneda), new("Explicación"),
        };
        var porEmpleado = lineas.ToLookup(l => l.RunEmployeeId);
        var tabla = new List<FilaExportable>(lineas.Count);
        foreach (var f in filas)
        {
            foreach (var l in porEmpleado[f.RunEmployeeId].OrderBy(l => l.Order))
            {
                tabla.Add(new FilaExportable(
                    [f.Nombre, f.Documento, l.ConceptName + (l.AffectsAccounting ? string.Empty : " (sin asiento)"), l.ConceptCode,
                     ReportesDeNomina.NombreNaturaleza(l.Nature.ToString()), l.Quantity, l.BaseAmount, l.Amount, ReportesDeLiquidaciones.ResumenDe(l.ExplanationJson)],
                    f.Nombre));
            }
        }
        var totales = new FilaExportable(["Total", $"{filas.Count} empleado(s)", "Neto a pagar", null, null, null, null, filas.Sum(f => f.NetPay), null], Resaltada: true);
        var notas = new List<string>
        {
            $"Devengos {filas.Sum(f => f.TotalEarnings):N0} · Deducciones {filas.Sum(f => f.TotalDeductions):N0} · Neto {filas.Sum(f => f.NetPay):N0}. Corte {run.CutoffDate:dd/MM/yyyy}.",
            "Cada línea trae el resumen de su explicación; el paso a paso completo está en la pantalla, en el detalle del empleado.",
        };
        var resultado = new TablaExportable(
            $"{ReportesDeLiquidaciones.Titulo(run)} · detalle por empleado y concepto",
            ReportesDeLiquidaciones.Subtitulo(run, tenant.TenantName ?? "Cooperativa", user.UserName ?? "sistema", clock.UtcNow, filas.Count),
            columnas, tabla, totales, notas);

        if (request.Exportacion)
            await audit.EmitAsync(AuditEventTypes.PayrollRunExported, nameof(PayrollRun), run.PublicId, null,
                new { report = "liquidacion-especial-detalle", kind = run.Kind.ToString(), version = run.Version, employees = filas.Count }, ct);
        return Result.Success(resultado);
    }
}
