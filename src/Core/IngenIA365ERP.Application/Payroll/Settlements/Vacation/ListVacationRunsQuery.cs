using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Vacation;

/// <summary>Las liquidaciones de vacaciones (contracts/api.md §3.3 <c>GET /</c>): por empleado, año del corte y estado; las reemplazadas no se listan salvo que se pidan.</summary>
public sealed record ListVacationRunsQuery(Guid? EmployeePublicId = null, int? Year = null, string? Status = null, bool IncludeSuperseded = false)
    : IRequest<Result<IReadOnlyList<VacationRunRowDto>>>;

public sealed class ListVacationRunsQueryValidator : AbstractValidator<ListVacationRunsQuery>
{
    public ListVacationRunsQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).When(x => x.Year is not null);
        RuleFor(x => x.Status).Must(s => Enum.TryParse<PayrollRunStatus>(s, true, out _)).When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("El estado no existe (Draft, Stale, Superseded, Approved, Reversed).");
    }
}

public sealed class ListVacationRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListVacationRunsQuery, Result<IReadOnlyList<VacationRunRowDto>>>
{
    public async Task<Result<IReadOnlyList<VacationRunRowDto>>> Handle(ListVacationRunsQuery request, CancellationToken ct)
    {
        var corridas = db.PayrollRuns.AsNoTracking()
            .Include(r => r.AccountingDocument)
            .Where(r => r.Kind == PayrollRunKind.Vacation);
        if (request.EmployeePublicId is { } empleadoId)
        {
            var id = await db.Employees.AsNoTracking().Where(e => e.PublicId == empleadoId).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
            if (id is null) return Result.Failure<IReadOnlyList<VacationRunRowDto>>(SettlementErrors.EmployeeNotFound);
            corridas = corridas.Where(r => r.EmployeeId == id);
        }
        if (request.Year is { } anio) corridas = corridas.Where(r => r.CutoffDate!.Value.Year == anio);
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<PayrollRunStatus>(request.Status, true, out var estado))
            corridas = corridas.Where(r => r.Status == estado);
        else if (!request.IncludeSuperseded)
            corridas = corridas.Where(r => r.Status != PayrollRunStatus.Superseded);

        var filas = await corridas.OrderByDescending(r => r.CutoffDate).ThenByDescending(r => r.Version).ToListAsync(ct);
        var idsMovimiento = filas.Where(r => r.VacationMovementId != null).Select(r => r.VacationMovementId!.Value).Distinct().ToList();
        var movimientos = await db.VacationMovements.AsNoTracking().Where(m => idsMovimiento.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var idsEmpleado = filas.Where(r => r.EmployeeId != null).Select(r => r.EmployeeId!.Value).Distinct().ToList();
        var empleados = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where idsEmpleado.Contains(e.Id)
            select new { e.Id, e.PublicId, Nombre = (p.FirstName + " " + p.LastName).Trim(), p.TaxId }
        ).ToDictionaryAsync(x => x.Id, ct);

        var lista = new List<VacationRunRowDto>(filas.Count);
        foreach (var r in filas)
        {
            var m = r.VacationMovementId is { } mid ? movimientos.GetValueOrDefault(mid) : null;
            var e = r.EmployeeId is { } eid ? empleados.GetValueOrDefault(eid) : null;
            var avisos = new List<RunWarningDto>();
            if (m is { Status: VacationMovementStatus.Cancelled })
                avisos.Add(new RunWarningDto("Payroll.Vacation.MovementCancelled", "El movimiento de esta liquidación está anulado.", new { movementPublicId = m.PublicId }));
            lista.Add(new VacationRunRowDto(
                r.PublicId, m?.PublicId, e?.PublicId ?? Guid.Empty, e?.Nombre ?? string.Empty, e?.TaxId ?? string.Empty,
                m?.Kind, m?.StartDate, m is { Kind: VacationMovementKind.Enjoyment } ? m.EndDate : null,
                m?.BusinessDays ?? 0m, m?.CalendarDays ?? 0,
                m is { Kind: VacationMovementKind.Compensation } ? m.BusinessDays : null,
                r.TotalNet, r.Status.ToString(), r.Version, r.CutoffDate!.Value, r.PayDate, r.CalculatedAt, r.CalculatedBy, r.ApprovedAt, r.ApprovedBy,
                r.AccountingDocument?.PublicId, r.AccountingDocument?.Referencia(), avisos));
        }
        return Result.Success<IReadOnlyList<VacationRunRowDto>>(lista);
    }
}
