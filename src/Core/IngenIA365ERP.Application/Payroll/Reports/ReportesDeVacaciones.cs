using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Feature 010 US4 (contracts/api.md §11): las dos vistas de vacaciones del centro de reportes como
/// <see cref="TablaExportable"/>. <c>saldos-vacaciones</c> se apoya en la misma consulta de la
/// pantalla —mismos números—; <c>movimientos-vacaciones</c> lista los movimientos de un rango con
/// su estado y su corrida. Ninguna calcula nada.
/// </summary>
public static class ReportesDeVacaciones
{
    public static string NombreTipo(VacationMovementKind kind) => kind switch
    {
        VacationMovementKind.Enjoyment => "Disfrute",
        VacationMovementKind.Compensation => "Compensación en dinero",
        VacationMovementKind.Adjustment => "Ajuste",
        VacationMovementKind.SettlementPayout => "Pago al retiro",
        _ => kind.ToString(),
    };

    public static string NombreEstado(VacationMovementStatus status) => status switch
    {
        VacationMovementStatus.Registered => "Registrado",
        VacationMovementStatus.Liquidated => "Liquidado",
        VacationMovementStatus.Cancelled => "Anulado",
        _ => status.ToString(),
    };
}

// ------------------------------------------------------------ saldos-vacaciones --

public sealed record SaldosVacacionesReportQuery(DateOnly? AsOf = null, string? Search = null) : IRequest<Result<TablaExportable>>;

public sealed class SaldosVacacionesReportQueryValidator : AbstractValidator<SaldosVacacionesReportQuery>
{
    public SaldosVacacionesReportQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class SaldosVacacionesReportQueryHandler(ISender sender, IDateTimeService clock) : IRequestHandler<SaldosVacacionesReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(SaldosVacacionesReportQuery request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? clock.TodayUtc;
        var saldos = await sender.Send(new GetVacationBalancesQuery(asOf, request.Search), ct);
        if (saldos.IsFailure) return Result.Failure<TablaExportable>(saldos.Error);

        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Ingreso", TipoDeColumna.Fecha),
            new("Días trabajados", TipoDeColumna.Entero), new("Suspensiones", TipoDeColumna.Entero),
            new("Causados", TipoDeColumna.Decimal), new("Saldo inicial", TipoDeColumna.Decimal), new("Disfrutados", TipoDeColumna.Decimal),
            new("Compensados", TipoDeColumna.Decimal), new("Ajustes", TipoDeColumna.Decimal), new("Pagados al retiro", TipoDeColumna.Decimal),
            new("Pendientes", TipoDeColumna.Decimal), new("Último disfrute hasta", TipoDeColumna.Fecha),
        };
        var filas = saldos.Value.Select(s => new FilaExportable(
        [
            s.Name, s.Document, s.HireDate, s.WorkedDays, s.SuspensionDays, s.AccruedDays, s.OpeningDays, s.EnjoyedDays, s.CompensatedDays,
            s.AdjustedDays, s.SettlementPaidDays, s.PendingDays, s.LastEnjoymentTo?.ToDateTime(TimeOnly.MinValue),
        ])).ToList();
        var totales = new FilaExportable(
        [
            "Total", $"{saldos.Value.Count} empleado(s)", null, null, null,
            saldos.Value.Sum(s => s.AccruedDays), saldos.Value.Sum(s => s.OpeningDays), saldos.Value.Sum(s => s.EnjoyedDays),
            saldos.Value.Sum(s => s.CompensatedDays), saldos.Value.Sum(s => s.AdjustedDays), saldos.Value.Sum(s => s.SettlementPaidDays),
            saldos.Value.Sum(s => s.PendingDays), null,
        ], Resaltada: true);
        var notas = new List<string>
        {
            "Saldo derivado, nunca almacenado: días trabajados (calendario comercial, menos suspensiones del contrato) × días de vacaciones por año del parámetro VACACIONES_DIAS_ANIO / 360, más el saldo inicial digitado, menos lo disfrutado, compensado y pagado al retiro, más o menos los ajustes.",
            "Los días son hábiles según la semana laboral de la empresa (política SemanaLaboral) y el calendario de festivos.",
        };
        return Result.Success(new TablaExportable("Saldos de vacaciones", $"Al {asOf:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(request.Search) ? string.Empty : $" · filtro «{request.Search}»"),
            columnas, filas, totales, notas));
    }
}

// -------------------------------------------------------- movimientos-vacaciones --

public sealed record MovimientosVacacionesReportQuery(DateOnly Desde, DateOnly Hasta, Guid? EmployeePublicId = null) : IRequest<Result<TablaExportable>>;

public sealed class MovimientosVacacionesReportQueryValidator : AbstractValidator<MovimientosVacacionesReportQuery>
{
    public MovimientosVacacionesReportQueryValidator()
    {
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde).WithMessage("La fecha final debe ser igual o posterior a la inicial.");
    }
}

public sealed class MovimientosVacacionesReportQueryHandler(IApplicationDbContext db) : IRequestHandler<MovimientosVacacionesReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(MovimientosVacacionesReportQuery request, CancellationToken ct)
    {
        var movimientos = db.VacationMovements.AsNoTracking()
            .Where(m => m.StartDate >= request.Desde && m.StartDate <= request.Hasta);
        string? nombreFiltro = null;
        if (request.EmployeePublicId is { } empleadoId)
        {
            var e = await db.Employees.AsNoTracking().Include(x => x.Person).FirstOrDefaultAsync(x => x.PublicId == empleadoId, ct);
            if (e is null) return Result.Failure<TablaExportable>(SettlementErrors.EmployeeNotFound);
            movimientos = movimientos.Where(m => m.EmployeeId == e.Id);
            nombreFiltro = NombreDePersona.Completo(e.Person);
        }
        var filasMov = await movimientos.OrderBy(m => m.StartDate).ThenBy(m => m.Id).ToListAsync(ct);

        var idsEmpleado = filasMov.Select(m => m.EmployeeId).Distinct().ToList();
        var empleados = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where idsEmpleado.Contains(e.Id)
            select new { e.Id, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId }
        ).ToDictionaryAsync(x => x.Id, ct);
        var idsMov = filasMov.Select(m => m.Id).ToList();
        var corridas = await db.PayrollRuns.AsNoTracking().Include(r => r.AccountingDocument)
            .Where(r => r.VacationMovementId != null && idsMov.Contains(r.VacationMovementId.Value) && r.Status != PayrollRunStatus.Superseded)
            .OrderByDescending(r => r.Version).ToListAsync(ct);
        var corridaPorMov = corridas.GroupBy(r => r.VacationMovementId!.Value).ToDictionary(g => g.Key, g => g.First());

        var columnas = new List<ColumnaExportable>
        {
            new("Empleado"), new("Documento"), new("Tipo"), new("Desde", TipoDeColumna.Fecha), new("Hasta", TipoDeColumna.Fecha),
            new("Días hábiles", TipoDeColumna.Decimal), new("Días calendario", TipoDeColumna.Entero), new("Semana laboral"),
            new("Estado"), new("Liquidación"), new("Valor", TipoDeColumna.Moneda), new("Comprobante"), new("Registrado por"), new("Registrado el", TipoDeColumna.Fecha), new("Notas"),
        };
        var filas = filasMov.Select(m =>
        {
            var e = empleados.GetValueOrDefault(m.EmployeeId);
            var r = corridaPorMov.GetValueOrDefault(m.Id);
            return new FilaExportable(
            [
                e is null ? string.Empty : NombreDePersona.Completo(e.FirstName, e.OtherNames, e.LastName, e.SecondLastName), e?.TaxId ?? string.Empty, ReportesDeVacaciones.NombreTipo(m.Kind),
                m.StartDate.ToDateTime(TimeOnly.MinValue), m.EndDate?.ToDateTime(TimeOnly.MinValue),
                m.BusinessDays, m.CalendarDays, m.WeekPolicyUsed, ReportesDeVacaciones.NombreEstado(m.Status),
                r is null ? string.Empty : $"v{r.Version} · {r.Status}", r?.TotalNet, r?.AccountingDocument is { } d ? d.Referencia() : string.Empty,
                m.CreatedBy ?? string.Empty, m.CreatedAt, m.Status == VacationMovementStatus.Cancelled ? $"Anulado: {m.CancelReason}" : m.Notes,
            ], ReportesDeVacaciones.NombreTipo(m.Kind));
        }).ToList();
        var vivos = filasMov.Where(m => m.Status != VacationMovementStatus.Cancelled).ToList();
        var totales = new FilaExportable(
        [
            "Total", $"{filasMov.Count} movimiento(s)", null, null, null,
            vivos.Where(m => m.Kind != VacationMovementKind.Adjustment).Sum(m => m.BusinessDays), vivos.Sum(m => m.CalendarDays), null, null, null,
            corridaPorMov.Values.Where(r => r.Status == PayrollRunStatus.Approved).Sum(r => r.TotalNet), null, null, null, null,
        ], Resaltada: true);
        var notas = new List<string> { "Los anulados se listan con su motivo y no suman en los totales. El valor es el neto de la liquidación vigente del movimiento." };
        return Result.Success(new TablaExportable("Movimientos de vacaciones",
            $"Del {request.Desde:dd/MM/yyyy} al {request.Hasta:dd/MM/yyyy}" + (nombreFiltro is null ? string.Empty : $" · {nombreFiltro}"),
            columnas, filas, totales, notas));
    }
}
