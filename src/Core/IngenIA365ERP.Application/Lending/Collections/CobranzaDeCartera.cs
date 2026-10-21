using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>Un crédito como lo nombran las respuestas de cobranza (api.md §1.7): su <c>PublicId</c> y su número. (nuevo)</summary>
public sealed record CreditoRefDto(Guid LoanPublicId, string Number);

/// <summary>
/// Las lecturas que comparten los comandos, las consultas y el ciclo de la cobranza de K4 (feature 013, US12; data-model §26): el crédito con su
/// alcance, las etapas vigentes, la etapa y el gestor vigentes de cada crédito, los pagos vigentes (para promesas y acuerdos), lo vencido y quiénes
/// están obligados. <b>Nunca</b> lee referencias personales: sólo el deudor y los codeudores del crédito (<c>LaCobranzaNuncaContactaReferencias</c>).
/// (nuevo)
/// </summary>
public static class CobranzaDeCartera
{
    /// <summary>
    /// El crédito por su <c>PublicId</c> dentro del alcance de quien opera; fuera o inexistente, el mismo 404 (<c>Lending.Loan.NotFound</c>) y el
    /// evento <c>Lending.Access.OutOfScope</c>.
    /// </summary>
    public static async Task<Result<Loan>> CreditoAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Guid loanPublicId,
        CancellationToken ct)
    {
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == loanPublicId && !l.IsDeleted, ct);
        if (credito is null) return Result.Failure<Loan>(Settlement.LecturaDelCredito.Inexistente());
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, Settlement.LecturaDelCredito.Inexistente(), auditoria, "Loan",
            credito.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<Loan>(dentro.Error) : Result.Success(credito);
    }

    /// <summary>Las versiones de las etapas vigentes el <paramref name="dia"/>, en su orden.</summary>
    public static async Task<IReadOnlyList<CollectionStage>> EtapasVigentesAsync(IApplicationDbContext db, DateOnly dia, CancellationToken ct) =>
        (await db.CollectionStages.AsNoTracking().Where(s => !s.IsDeleted && s.ValidFrom <= dia && (s.ValidTo == null || s.ValidTo >= dia))
            .ToListAsync(ct))
        .OrderBy(s => s.Order).ThenBy(s => s.Code, StringComparer.Ordinal).ToList();

    /// <summary>
    /// La etapa vigente de cada crédito: su última entrada, salvo que sea una salida de cobranza (K4, bloque D). Un crédito que salió —o que nunca
    /// entró— no está en el diccionario: está fuera de cobranza.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, CollectionStageHistoryEntry>> EtapaVigenteAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds,
        CancellationToken ct) =>
        (await UltimaEntradaAsync(db, loanIds, ct)).Where(e => !e.Value.EsSalida).ToDictionary(e => e.Key, e => e.Value);

    /// <summary>La última entrada de la historia de etapas de cada crédito, salidas incluidas (la que mira el ciclo diario).</summary>
    public static async Task<IReadOnlyDictionary<int, CollectionStageHistoryEntry>> UltimaEntradaAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds,
        CancellationToken ct)
    {
        if (loanIds.Count == 0) return new Dictionary<int, CollectionStageHistoryEntry>();
        var ids = loanIds.Distinct().ToArray();
        var entradas = await db.CollectionStageHistory.AsNoTracking().Where(h => ids.Contains(h.LoanId) && !h.IsDeleted).ToListAsync(ct);
        var locales = db.CollectionStageHistory.Local.Where(h => ids.Contains(h.LoanId));
        return entradas.Concat(locales).GroupBy(h => h.LoanId)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.EnteredOn).ThenBy(h => h.Id == 0 ? int.MaxValue : h.Id).Last());
    }

    /// <summary>
    /// La asignación vigente de cada crédito: la última del crédito o, si la última es del deudor completo (crédito nulo) y es más reciente, ésa. Si la
    /// última es un cierre (sin gestor, K4 bloque D), el crédito no tiene gestor vigente.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, CollectorAssignment>> GestorVigenteAsync(IApplicationDbContext db,
        IReadOnlyCollection<(int LoanId, int PersonId)> creditos, CancellationToken ct)
    {
        if (creditos.Count == 0) return new Dictionary<int, CollectorAssignment>();
        var loanIds = creditos.Select(c => c.LoanId).Distinct().ToArray();
        var personas = creditos.Select(c => c.PersonId).Distinct().ToArray();
        var guardadas = await db.CollectorAssignments.AsNoTracking()
            .Where(a => !a.IsDeleted && ((a.LoanId != null && loanIds.Contains(a.LoanId.Value)) || (a.LoanId == null && personas.Contains(a.PersonId))))
            .ToListAsync(ct);
        var todas = guardadas.Concat(db.CollectorAssignments.Local.Where(a => a.LoanId is { } l ? loanIds.Contains(l) : personas.Contains(a.PersonId))).ToList();
        var salida = new Dictionary<int, CollectorAssignment>();
        foreach (var (loanId, personId) in creditos)
        {
            var vigente = todas.Where(a => a.LoanId == loanId || (a.LoanId == null && a.PersonId == personId))
                .OrderBy(a => a.AssignedOn).ThenBy(a => a.Id == 0 ? int.MaxValue : a.Id).LastOrDefault();
            if (vigente is { EsCierre: false }) salida[loanId] = vigente;
        }
        return salida;
    }

    /// <summary>Los pagos vigentes (no reversados) de los créditos, como los miran las promesas y los acuerdos (fecha valor, monto, registro).</summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<PagoParaCobranza>>> PagosVigentesAsync(IApplicationDbContext db,
        IReadOnlyCollection<int> loanIds, CancellationToken ct)
    {
        if (loanIds.Count == 0) return new Dictionary<int, IReadOnlyList<PagoParaCobranza>>();
        var ids = loanIds.Distinct().ToArray();
        var pagos = await db.LoanOperations.AsNoTracking()
            .Where(o => ids.Contains(o.LoanId) && (o.Kind == LoanOperationKind.Payment || o.Kind == LoanOperationKind.ProvisionalCollection)
                        && o.ReversedByOperationId == null && o.ReversesOperationId == null)
            .Select(o => new { o.LoanId, o.ValueDate, o.Amount, o.RegisteredAt })
            .ToListAsync(ct);
        return ids.ToDictionary(id => id, id => (IReadOnlyList<PagoParaCobranza>)pagos.Where(p => p.LoanId == id)
            .Select(p => new PagoParaCobranza(p.ValueDate, p.Amount, p.RegisteredAt)).ToList());
    }

    /// <summary>
    /// Lo vencido a la fecha del estado: todo lo cobrable de las cuotas vencidas y los cargos sin cuota (gastos de cobranza, otros cargos, seguros
    /// causados sin cuota). Es la base de un acuerdo de pago (FR-120) y del gasto de cobranza sobre lo vencido.
    /// </summary>
    public static decimal ValorVencido(EstadoDelCredito estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var vencidas = estado.Cuotas.Where(c => c.Vencida).Select(c => c.Numero).ToHashSet();
        return estado.Saldos
            .Where(s => s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance) && s.Valor > 0m)
            .Where(s => s.Cuota is { } q ? vencidas.Contains(q) : s.Concepto != LoanConcept.Principal)
            .Sum(s => s.Valor);
    }

    /// <summary>
    /// Los días para la etapa: los de mora si el crédito está en mora; si está al día, los que faltan para el próximo vencimiento con signo menos
    /// (la etapa preventiva admite negativos, data-model §26.4); nulo si no queda cuota por vencer.
    /// </summary>
    public static int? DiasParaEtapa(EstadoDelCredito estado, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (estado.AlturaDeMora.Dias > 0) return estado.AlturaDeMora.Dias;
        var proxima = estado.Cuotas.Where(c => !c.Vencida && c.Vencimiento >= dia).OrderBy(c => c.Vencimiento).FirstOrDefault();
        return proxima is null ? null : -(proxima.Vencimiento.DayNumber - dia.DayNumber);
    }

    /// <summary>¿La persona es el deudor o un codeudor vigente del crédito a <paramref name="hoy"/>? Devuelve su papel, o nulo si no está obligada.</summary>
    public static async Task<ContactRole?> PapelAsync(IApplicationDbContext db, Loan credito, int personId, DateOnly hoy, CancellationToken ct)
    {
        if (credito.DebtorPersonId == personId) return ContactRole.Debtor;
        var codeudor = await db.LoanParties.AsNoTracking().AnyAsync(p => p.LoanId == credito.Id && p.PersonId == personId && !p.IsDeleted
            && p.Role == CreditPartyRole.CoDebtor && (p.ReleasedOn == null || p.ReleasedOn > hoy) && (p.ExtinguishedOn == null || p.ExtinguishedOn > hoy), ct);
        return codeudor ? ContactRole.CoDebtor : null;
    }

    /// <summary>La pagaduría vigente del crédito (<c>LND_LoanPayrollLinks</c>), o nula.</summary>
    public static async Task<int?> PagaduriaAsync(IApplicationDbContext db, int loanId, CancellationToken ct) =>
        await db.LoanPayrollLinks.AsNoTracking().Where(v => v.LoanId == loanId && v.ValidTo == null && !v.IsDeleted)
            .Select(v => v.PaymasterPersonId).FirstOrDefaultAsync(ct);

    /// <summary>Un usuario activo de la cooperativa por su <c>PublicId</c> (<c>SEC_Users.Id</c>), o nulo.</summary>
    public static async Task<int?> UsuarioAsync(IApplicationDbContext db, Guid userPublicId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.PublicId == userPublicId && !u.IsDeleted && u.IsActive).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);

    /// <summary>¿El deudor está en una negociación de insolvencia a <paramref name="dia"/>? (FR-098)</summary>
    public static async Task<InsolvencyProceedingRef?> InsolvenciaAsync(IApplicationDbContext db, int personId, DateOnly dia, CancellationToken ct) =>
        await db.InsolvencyProceedings.AsNoTracking()
            .Where(p => p.PersonId == personId && !p.IsDeleted && p.Status == InsolvencyProceedingStatus.Negotiating && p.StartDate <= dia)
            .OrderByDescending(p => p.StartDate).Select(p => new InsolvencyProceedingRef(p.Id, p.PublicId)).FirstOrDefaultAsync(ct);

    /// <summary>El instante local (Colombia) de un instante UTC, con el desfase del reloj.</summary>
    public static DateTime ALocal(DateTime utc, TimeSpan desfase) => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + desfase, DateTimeKind.Unspecified);
}

/// <summary>Una insolvencia en negociación, por su Id y su <c>PublicId</c>. (nuevo)</summary>
public sealed record InsolvencyProceedingRef(int Id, Guid PublicId);
