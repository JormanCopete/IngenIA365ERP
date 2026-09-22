using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Feature 010 US2 (FR-012; contracts/api.md §11, contracts/archivos.md §3.1): la relación de
/// consignación de cesantías por fondo como <see cref="TablaExportable"/>. Un bloque (sección) por
/// fondo ordenado por nombre y luego por apellido; total por fondo y total general, que debe
/// igualar la cuenta por pagar al fondo del comprobante; en Excel cada fondo va además en su
/// propia hoja para entregársela. Los números son los de <see cref="DepositScheduleBuilder"/>: los
/// mismos que la pantalla y que <c>mark-deposited</c>. <see cref="Formato"/> viene de la ruta sólo
/// para auditar la exportación a archivo (<c>Payroll.Run.Exported</c>); el JSON de la pantalla no se audita.
/// </summary>
public sealed record ConsignacionCesantiasReportQuery(Guid RunPublicId, Guid? FundPublicId = null, string? Formato = null) : IRequest<Result<TablaExportable>>;

public sealed class ConsignacionCesantiasReportQueryValidator : AbstractValidator<ConsignacionCesantiasReportQuery>
{
    public ConsignacionCesantiasReportQueryValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.FundPublicId).NotEqual(Guid.Empty).When(x => x.FundPublicId is not null);
        RuleFor(x => x.Formato).MaximumLength(10);
    }
}

public sealed class ConsignacionCesantiasReportQueryHandler(IApplicationDbContext db, ICurrentTenantService tenant, ICurrentUserService user, IDateTimeService clock, PayrollAuditEmitter audit)
    : IRequestHandler<ConsignacionCesantiasReportQuery, Result<TablaExportable>>
{
    public const string Vista = "consignacion-cesantias";

    public async Task<Result<TablaExportable>> Handle(ConsignacionCesantiasReportQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<TablaExportable>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Severance) return Result.Failure<TablaExportable>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Severance));

        var relacion = await new DepositScheduleBuilder(db).BuildAsync(run, ct);
        var bloques = relacion.Funds.AsEnumerable();
        if (request.FundPublicId is { } fondo)
        {
            bloques = bloques.Where(b => b.FundPublicId == fondo).ToList();
            if (!bloques.Any())
            {
                var existe = await db.SeveranceProviders.AsNoTracking().AnyAsync(f => f.PublicId == fondo, ct);
                if (!existe) return Result.Failure<TablaExportable>(SeveranceErrors.FundNotFound);
            }
        }
        var lista = bloques.ToList();

        // Encabezado de la cooperativa como en los comprobantes del empleado (PayslipModelBuilder): el nombre del tenant.
        var encabezado = string.IsNullOrWhiteSpace(tenant.TenantName) ? string.Empty : $"{tenant.TenantName} · ";

        var columnas = new List<ColumnaExportable>
        {
            new("Tipo doc."), new("Documento"), new("Apellidos y nombres"), new("Fecha de ingreso", TipoDeColumna.Fecha),
            new("Salario base de liquidación", TipoDeColumna.Moneda), new("Días liquidados", TipoDeColumna.Decimal),
            new("Cesantías a consignar", TipoDeColumna.Moneda), new("Intereses (se pagan al empleado)", TipoDeColumna.Moneda), new("Estado"),
        };

        var filas = new List<FilaExportable>();
        foreach (var b in lista)
        {
            var seccion = Seccion(b);
            var estado = b.Deposited
                ? $"Consignado el {b.DepositedAt:dd-MM-yyyy}" + (string.IsNullOrWhiteSpace(b.Reference) ? string.Empty : $", ref. {b.Reference}")
                : "Pendiente";
            foreach (var l in b.Lines.OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase))
                filas.Add(new FilaExportable([l.DocumentType, l.Document, l.Name, l.HireDate.ToDateTime(TimeOnly.MinValue), l.BaseSalary, l.Days, l.Amount, l.Interest, estado], seccion));
            filas.Add(new FilaExportable([string.Empty, string.Empty, $"Total {b.FundName} · {b.Employees} empleado(s)", null, null, null, b.Total, b.InterestTotal, estado], seccion, Resaltada: true));
        }

        var totales = new FilaExportable(["Total general", string.Empty, $"{lista.Sum(b => b.Employees)} empleado(s) en {lista.Count} fondo(s)", null, null, null,
            lista.Sum(b => b.Total), lista.Sum(b => b.InterestTotal), string.Empty], Resaltada: true);

        var notas = new List<string>
        {
            $"Corrida v{run.Version} · {EstadoTexto(run.Status)} · calculada por {run.CalculatedBy} el {run.CalculatedAt:dd/MM/yyyy HH:mm}"
            + (run.ApprovedAt is { } a ? $" · aprobada por {run.ApprovedBy} el {a:dd/MM/yyyy HH:mm}" : string.Empty),
            relacion.DueDate is { } limite
                ? $"Fecha límite de consignación al fondo: {limite:dd/MM/yyyy} (CESANTIAS_FECHA_LIMITE_CONSIGNACION)."
                : "Sin fecha límite de consignación: el parámetro CESANTIAS_FECHA_LIMITE_CONSIGNACION no tiene vigencia al corte.",
            relacion.InterestDueDate is { } limiteInt
                ? $"Los intereses se pagan al empleado, no al fondo; fecha límite {limiteInt:dd/MM/yyyy} (INT_CESANTIAS_FECHA_LIMITE)."
                : "Los intereses se pagan al empleado, no al fondo.",
            "El total general debe igualar la cuenta por pagar a los fondos del comprobante contable de la liquidación.",
            $"Generado por {user.UserName} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC.",
        };

        var tabla = new TablaExportable(
            $"{encabezado}Consignación de cesantías año {relacion.Year}",
            request.FundPublicId is null ? $"Relación por fondo · corte {relacion.CutoffDate:dd/MM/yyyy} · {lista.Count} fondo(s)" : $"Fondo {lista.FirstOrDefault()?.FundName ?? "—"} · corte {relacion.CutoffDate:dd/MM/yyyy}",
            columnas, filas, totales, notas) { HojaPorSeccion = true };

        if (!string.IsNullOrWhiteSpace(request.Formato) && !request.Formato.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await audit.EmitAsync(AuditEventTypes.PayrollRunExported, "PayrollRun", run.PublicId, null, new
            {
                report = Vista, format = request.Formato.ToLowerInvariant(), runPublicId = run.PublicId, fundPublicId = request.FundPublicId,
                funds = lista.Count, employees = lista.Sum(b => b.Employees), total = lista.Sum(b => b.Total),
            }, ct);
        }

        return Result.Success(tabla);
    }

    private static string Seccion(DepositScheduleFundDto b)
    {
        var nit = string.IsNullOrWhiteSpace(b.FundNit) ? string.Empty : $" · NIT {b.FundNit}";
        var pila = string.IsNullOrWhiteSpace(b.PilaCode) ? string.Empty : $" · PILA {b.PilaCode}";
        return $"{b.FundName}{nit}{pila}";
    }

    private static string EstadoTexto(PayrollRunStatus status) => status switch
    {
        PayrollRunStatus.Draft => "Borrador",
        PayrollRunStatus.Stale => "Desactualizado",
        PayrollRunStatus.Superseded => "Reemplazado",
        PayrollRunStatus.Approved => "Aprobado",
        PayrollRunStatus.Reversed => "Reversado",
        _ => status.ToString(),
    };
}
