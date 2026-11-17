using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Cartera;

// 2026-11-17 (pedido del dueño): lo que espera en Cartera a quien tiene el permiso, con el alcance de oficina y línea de quien mira.

/// <summary>Solicitudes de crédito y de cupo que esperan decisión en las oficinas y líneas de quien mira (la lista de <c>/cartera/solicitudes</c>).</summary>
public sealed class SolicitudesEnDecision(IApplicationDbContext db, IAlcanceDeCartera alcance) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "cartera.solicitudes-en-decision", "Solicitudes de crédito esperando decisión", "Cartera", "Lending.Applications.Read", "/cartera/solicitudes");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var deAlcance = await alcance.ObtenerAsync(ct);
        return await db.CreditApplications.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status == CreditApplicationStatus.PendingDecision)
            .PorOficinaYLinea(deAlcance, a => a.BranchId, a => a.CreditLineId)
            .CountAsync(ct);
    }
}

/// <summary>
/// «Mis pendientes» de Cartera (<c>/cartera/aprobaciones</c>): solicitudes, cupos, novedades y condonaciones que el usuario puede decidir
/// ahora (<c>canDecide</c>: tiene el nivel, está en su alcance y la segregación no lo excluye). Es la misma consulta de la pantalla
/// (<see cref="ListPendingLendingDecisionsQuery"/>); lo que va a sesión de un órgano no cuenta: se decide en Actas.
/// </summary>
public sealed class MisDecisionesDeCartera(ISender sender) : IPendienteDelTablero
{
    /// <summary>Tope de páginas que se recorren: un tablero no debe quedarse leyendo indefinidamente.</summary>
    private const int MaximoDePaginas = 20;

    public DefinicionDePendiente Definicion { get; } = new(
        "cartera.mis-decisiones", "Decisiones de Cartera por tomar", "Cartera", "Lending.Applications.Read", "/cartera/aprobaciones");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var cuenta = 0;
        for (var pagina = 1; pagina <= MaximoDePaginas; pagina++)
        {
            var r = await sender.Send(new ListPendingLendingDecisionsQuery(null, pagina, PageRequest.MaxPageSize), ct);
            if (r.IsFailure || r.Value is null) return cuenta;
            cuenta += r.Value.Items.Count(i => i.CanDecide);
            if (r.Value.Items.Count == 0 || (long)pagina * r.Value.PageSize >= r.Value.TotalCount) break;
        }
        return cuenta;
    }
}

/// <summary>
/// Solicitudes de crédito aprobadas, con la aprobación vigente, que todavía no se desembolsan (el paso de <c>/cartera/desembolsos</c>). Las de
/// cupo rotativo no cuentan: un cupo se otorga, no se desembolsa.
/// </summary>
public sealed class AprobadasSinDesembolsar(IApplicationDbContext db, IAlcanceDeCartera alcance) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "cartera.aprobadas-sin-desembolsar", "Créditos aprobados sin desembolsar", "Cartera", "Lending.Disbursements.Execute", "/cartera/desembolsos");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var deAlcance = await alcance.ObtenerAsync(ct);
        var hoy = contexto.Hoy;
        return await db.CreditApplications.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Kind == ApplicationKind.Loan && a.Status == CreditApplicationStatus.Approved
                        && (a.ApprovalValidUntil == null || a.ApprovalValidUntil >= hoy))
            .PorOficinaYLinea(deAlcance, a => a.BranchId, a => a.CreditLineId)
            .CountAsync(ct);
    }
}
