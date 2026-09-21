using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Implementación de <see cref="IPayrollRunStaleMarker"/> sobre el mismo
/// <see cref="IApplicationDbContext"/> del comando que la invoca: cambia el estado en
/// memoria y deja que ese comando guarde. Sólo toca corridas <c>Draft</c>; una
/// <c>Stale</c> ya lo está, y las aprobadas o reemplazadas no se tocan (Principio XI).
/// </summary>
public sealed class PayrollRunStaleMarker(IApplicationDbContext db, ILogger<PayrollRunStaleMarker> logger) : IPayrollRunStaleMarker
{
    public async Task<int> MarkStaleAsync(int payPeriodId, string reason, CancellationToken ct)
    {
        var borradores = await db.PayrollRuns
            .Where(r => r.PayPeriodId == payPeriodId && r.Status == PayrollRunStatus.Draft)
            .ToListAsync(ct);
        return Marcar(borradores, reason);
    }

    public async Task<int> MarkAllDraftsStaleAsync(string reason, CancellationToken ct)
    {
        var borradores = await db.PayrollRuns
            .Where(r => r.Status == PayrollRunStatus.Draft)
            .ToListAsync(ct);
        return Marcar(borradores, reason);
    }

    public async Task<int> MarkSettlementDraftsStaleAsync(IReadOnlyCollection<int> employeeIds, DateOnly affectsFrom, string reason, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return 0;
        var ids = employeeIds.Distinct().ToList();
        var borradores = await db.PayrollRuns
            .Where(r => r.Kind != PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Draft
                        && r.CutoffDate != null && r.CutoffDate >= affectsFrom
                        && db.PayrollRunEmployees.Any(re => re.PayrollRunId == r.Id && ids.Contains(re.EmployeeId)))
            .ToListAsync(ct);
        return Marcar(borradores, reason);
    }

    private int Marcar(List<Domain.Entities.Payroll.Transactions.PayrollRun> borradores, string reason)
    {
        foreach (var run in borradores)
        {
            run.Status = PayrollRunStatus.Stale;
            logger.LogInformation("Corrida {Run} ({Kind}, período {Period}, corte {Cutoff}, v{Version}) marcada Stale: {Reason}",
                run.PublicId, run.Kind, run.PayPeriodId, run.CutoffDate, run.Version, reason);
        }
        return borradores.Count;
    }
}
