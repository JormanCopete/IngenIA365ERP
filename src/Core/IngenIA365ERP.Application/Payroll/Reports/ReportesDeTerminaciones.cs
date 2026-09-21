using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Feature 010, US3 (contracts/api.md §11): las dos vistas del centro de reportes que nacen con la
/// definitiva —<c>terminaciones</c> y <c>saldos-iniciales-prestaciones</c>— como
/// <see cref="TablaExportable"/>. Se apoyan en las consultas que ya usan las pantallas (los mismos
/// números) y sólo las reordenan en una tabla; no calculan nada.
/// </summary>
public static class ReportesDeTerminaciones
{
    public static string NombreEstado(TerminationStatus status) => status switch
    {
        TerminationStatus.Registered => "Registrada",
        TerminationStatus.Settled => "Liquidada",
        TerminationStatus.Reinstated => "Reintegrado",
        TerminationStatus.Cancelled => "Anulada",
        _ => status.ToString(),
    };

    public static string NombreContrato(DianContractType? tipo) => tipo switch
    {
        DianContractType.FixedTerm => "Término fijo",
        DianContractType.Indefinite => "Indefinido",
        DianContractType.WorkOrLabor => "Obra o labor",
        DianContractType.Apprenticeship => "Aprendizaje",
        DianContractType.Internship => "Pasantía",
        _ => "—",
    };
}

// ------------------------------------------------------------ 1. terminaciones --

/// <summary>Retiros entre dos fechas con motivo, indemnización, neto y estado (contracts/api.md §11 <c>terminaciones</c>).</summary>
public sealed record TerminacionesReportQuery(DateOnly Desde, DateOnly Hasta) : IRequest<Result<TablaExportable>>;

public sealed class TerminacionesReportQueryValidator : AbstractValidator<TerminacionesReportQuery>
{
    public TerminacionesReportQueryValidator()
    {
        RuleFor(x => x.Desde).NotEqual(default(DateOnly));
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde).WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class TerminacionesReportQueryHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<TerminacionesReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(TerminacionesReportQuery request, CancellationToken ct)
    {
        var todas = await sender.Send(new ListTerminationsQuery(), ct);
        if (todas.IsFailure) return Result.Failure<TablaExportable>(todas.Error);
        var filas = todas.Value.Where(t => t.TerminationDate >= request.Desde && t.TerminationDate <= request.Hasta).OrderBy(t => t.TerminationDate).ThenBy(t => t.EmployeeName).ToList();

        // La indemnización de cada terminación: la línea INDEMNIZACION de su corrida.
        var idsCorrida = filas.Where(f => f.RunPublicId is not null).Select(f => f.RunPublicId!.Value).ToList();
        var indemnizaciones = idsCorrida.Count == 0 ? new Dictionary<Guid, decimal>() : await (
            from l in db.PayrollRunLines.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            where idsCorrida.Contains(r.PublicId) && l.ConceptCode == WellKnownConceptCodes.Indemnity
            select new { r.PublicId, l.Amount }).ToDictionaryAsync(x => x.PublicId, x => x.Amount, ct);

        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Retiro", TipoDeColumna.Fecha), new("Motivo"), new("Genera indemnización"),
            new("Contrato"), new("Indemnización", TipoDeColumna.Moneda), new("Neto", TipoDeColumna.Moneda), new("Estado"), new("Liquidación"), new("Aprobada", TipoDeColumna.Fecha),
        };
        var tabla = filas.Select(f => new FilaExportable(
        [
            f.EmployeeName, f.Document, f.TerminationDate.ToDateTime(TimeOnly.MinValue), f.ReasonName, f.GeneratesSeverancePay ? "Sí" : "No",
            ReportesDeTerminaciones.NombreContrato(f.ContractType),
            f.RunPublicId is { } rid && indemnizaciones.TryGetValue(rid, out var ind) ? ind : 0m,
            f.Net, ReportesDeTerminaciones.NombreEstado(f.Status),
            f.RunVersion is { } v ? $"v{v} · {f.RunStatus}" : "—", f.ApprovedAt,
        ], ReportesDeTerminaciones.NombreEstado(f.Status))).ToList();
        var totales = new FilaExportable(["Total", "", null, "", "", "", filas.Sum(f => f.RunPublicId is { } rid && indemnizaciones.TryGetValue(rid, out var ind) ? ind : 0m), filas.Sum(f => f.Net), "", "", null], Resaltada: true);
        var notas = new List<string>
        {
            $"{filas.Count} terminación(es) entre {request.Desde:dd/MM/yyyy} y {request.Hasta:dd/MM/yyyy}: {filas.Count(f => f.Status == TerminationStatus.Settled)} liquidada(s), {filas.Count(f => f.Status == TerminationStatus.Registered)} en borrador, {filas.Count(f => f.Status == TerminationStatus.Reinstated)} reintegrada(s), {filas.Count(f => f.Status == TerminationStatus.Cancelled)} anulada(s).",
            "El neto es el de la última versión de la definitiva; la indemnización, su línea INDEMNIZACION (cero cuando el motivo no la genera).",
        };
        return Result.Success(new TablaExportable("Terminaciones de contrato", $"Retiros del {request.Desde:dd/MM/yyyy} al {request.Hasta:dd/MM/yyyy}", columnas, tabla, totales, notas));
    }
}

// ----------------------------------------------- 2. saldos iniciales de prestaciones --

/// <summary>Saldo inicial de prestaciones por empleado a una fecha (contracts/api.md §11 <c>saldos-iniciales-prestaciones</c>).</summary>
public sealed record SaldosInicialesPrestacionesReportQuery(DateOnly? AsOf = null) : IRequest<Result<TablaExportable>>;

public sealed class SaldosInicialesPrestacionesReportQueryValidator : AbstractValidator<SaldosInicialesPrestacionesReportQuery>
{
    public SaldosInicialesPrestacionesReportQueryValidator() { }
}

public sealed class SaldosInicialesPrestacionesReportQueryHandler(ISender sender) : IRequestHandler<SaldosInicialesPrestacionesReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(SaldosInicialesPrestacionesReportQuery request, CancellationToken ct)
    {
        var saldos = await sender.Send(new ListBenefitBalancesQuery(request.AsOf), ct);
        if (saldos.IsFailure) return Result.Failure<TablaExportable>(saldos.Error);
        var filas = saldos.Value.OrderBy(s => s.Name).ToList();

        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Ingreso", TipoDeColumna.Fecha), new("Anterior al arranque"), new("Corte", TipoDeColumna.Fecha),
            new("Vacaciones (días)", TipoDeColumna.Decimal), new("Cesantías", TipoDeColumna.Moneda), new("Intereses", TipoDeColumna.Moneda), new("Prima", TipoDeColumna.Moneda),
            new("Estado"), new("Consumido por"),
        };
        var tabla = filas.Select(s => new FilaExportable(
        [
            s.Name, s.Document, s.HireDate, s.HiredBeforeStart ? "Sí" : "No", s.AsOfDate?.ToDateTime(TimeOnly.MinValue),
            s.PendingVacationDays, s.AccruedSeverance, s.AccruedSeveranceInterest, s.AccruedServiceBonus,
            s.AsOfDate is null ? (s.HiredBeforeStart ? "Falta" : "No aplica") : s.IsEditable ? "Editable" : "Consumido",
            string.Join(", ", s.ConsumedBy.Select(c => c.Kind.ToString())),
        ], s.AsOfDate is null && s.HiredBeforeStart ? "Sin saldo" : "Con saldo")).ToList();
        var conSaldo = filas.Where(s => s.AsOfDate is not null).ToList();
        var totales = new FilaExportable(["Total", "", null, "", null, conSaldo.Sum(s => s.PendingVacationDays ?? 0m), conSaldo.Sum(s => s.AccruedSeverance ?? 0m), conSaldo.Sum(s => s.AccruedSeveranceInterest ?? 0m), conSaldo.Sum(s => s.AccruedServiceBonus ?? 0m), "", ""], Resaltada: true);
        var faltan = filas.Count(s => s.HiredBeforeStart && s.AsOfDate is null);
        var notas = new List<string>
        {
            $"{filas.Count} empleado(s); {conSaldo.Count} con saldo digitado" + (faltan > 0 ? $"; {faltan} ingresaron antes del arranque y no tienen saldo: su prima, cesantías o vacaciones saldrán cortas." : "."),
            "Un saldo consumido por una liquidación aprobada ya no se edita: se ajusta con motivo (Nómina › Saldos iniciales).",
        };
        return Result.Success(new TablaExportable("Saldos iniciales de prestaciones", request.AsOf is { } d ? $"Vigentes al {d:dd/MM/yyyy}" : "Vigentes a hoy", columnas, tabla, totales, notas));
    }
}
