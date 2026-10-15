using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Lo común de las novedades (feature 013, K3, T0878): el expediente de segregación, el prefijo del número, la lectura en el alcance —una novedad es
/// de la oficina y la línea de su crédito principal; fuera, el mismo 404 que inexistente (api.md §1.3)— y la respuesta completa. (nuevo)
/// </summary>
public static class NovedadesDeCartera
{
    /// <summary>El tipo de expediente de una novedad en <c>LND_CaseParticipations</c>.</summary>
    public const string Expediente = "LoanNovelty";

    /// <summary>El prefijo del número de la novedad (<c>LND_NumberSequences</c>, clase <c>LoanNovelty</c>).</summary>
    public const string Prefijo = "NOV-";

    /// <summary>El prefijo del comprobante de la operación <c>TermsChange</c>.</summary>
    public const string PrefijoDeOperacion = "NVD-";

    /// <summary>La clase del origen de los hechos de una novedad (mensajes.md §3).</summary>
    public const string ClaseDeOrigen = "TermsChange";

    /// <summary>
    /// La novedad y su crédito principal si existen y están en el alcance de quien pide; si no, <c>Lending.Novelty.NotFound</c> (y el evento
    /// <c>Lending.Access.OutOfScope</c> si existe pero está fuera). Con <paramref name="seguir"/>, la novedad viene con seguimiento.
    /// </summary>
    public static async Task<Result<(LoanNovelty Novedad, Loan Credito)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.LoanNovelties : db.LoanNovelties.AsNoTracking();
        var n = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        if (n is null) return Result.Failure<(LoanNovelty, Loan)>(ErroresDeNovedades.Inexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == n.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeNovedades.Inexistente(), auditoria, Expediente, n.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(LoanNovelty, Loan)>(dentro.Error) : Result.Success((n, credito));
    }

    /// <summary>La respuesta completa de una novedad (api.md §11.1, <c>LoanNoveltyDto</c>).</summary>
    public static async Task<LoanNoveltyDto> DtoAsync(IApplicationDbContext db, LoanNovelty n, CancellationToken ct)
    {
        var creditos = await (from x in db.LoanNoveltyLoans.AsNoTracking()
                              join l in db.Loans.AsNoTracking() on x.LoanId equals l.Id
                              where x.LoanNoveltyId == n.Id && !x.IsDeleted
                              orderby x.Role, l.Number
                              select new NoveltyLoanDto(l.PublicId, l.Number, x.Role)).ToListAsync(ct);
        var consentimientos = await db.LoanNoveltyConsents.AsNoTracking().Where(c => c.LoanNoveltyId == n.Id && !c.IsDeleted).OrderBy(c => c.Id).ToListAsync(ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, consentimientos.Select(c => c.PersonId), ct);
        var garantias = consentimientos.Where(c => c.CollateralId is not null).Select(c => c.CollateralId!.Value).ToList();
        var deGarantia = await db.Collaterals.AsNoTracking().IgnoreQueryFilters().Where(c => garantias.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);
        var decisiones = await DecisionesAsync(db, LendingDecisionSubjects.Novelty, n.PublicId, ct);
        var quien = await ReferenciasDeGobierno.UsuariosAsync(db, [n.RequestedByUserId], ct);
        var guardada = SimulacionGuardada.Leer(n.SimulationJson);
        var operacion = n.AppliedOperationId is { } op
            ? await db.LoanOperations.AsNoTracking().Where(o => o.Id == op).Select(o => (Guid?)o.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var enlazado = n.ResultingLoanId is { } r
            ? await db.Loans.AsNoTracking().Where(l => l.Id == r).Select(l => new { l.PublicId, l.Number }).FirstOrDefaultAsync(ct)
            : null;
        var insolvencia = n.InsolvencyProceedingId is { } i
            ? await db.InsolvencyProceedings.AsNoTracking().Where(x => x.Id == i).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return new LoanNoveltyDto(
            n.PublicId,
            n.Number,
            creditos,
            n.Mechanism,
            n.Cause,
            n.CauseDeterminationJson,
            n.Status,
            n.RequestReason,
            NewTermsInput.DeJson(n.ProposedTermsJson),
            guardada?.Simulation,
            consentimientos.Select(c => new NoveltyConsentDto(personas[c.PersonId], c.PartyRole, c.Status,
                c.CollateralId is { } g ? deGarantia.GetValueOrDefault(g) : null, c.SignedAttachmentPublicId, c.RecordedAt, c.RefusalResolution,
                c.ResolutionReason)).ToList(),
            guardada?.RoutingResult,
            decisiones,
            n.RatingAtRequest.ToString(),
            n.RestructuringCountAfter,
            insolvencia,
            quien.GetValueOrDefault(n.RequestedByUserId)?.Name ?? string.Empty,
            n.RequestedAt,
            operacion,
            enlazado?.PublicId,
            enlazado?.Number,
            n.AppliedAt,
            n.CancelledAt,
            n.CancelReason,
            n.RowVersion);
    }

    /// <summary>Las decisiones de un asunto (individuales y de acta), las más antiguas primero.</summary>
    public static async Task<IReadOnlyList<SubjectDecisionDto>> DecisionesAsync(IApplicationDbContext db, string sujeto, Guid publicId, CancellationToken ct)
    {
        var decisiones = await db.CreditDecisions.AsNoTracking().Where(d => d.SubjectType == sujeto && d.SubjectPublicId == publicId)
            .OrderBy(d => d.DecidedAt).ToListAsync(ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, decisiones.Where(d => d.DecidedByUserId is not null).Select(d => d.DecidedByUserId!.Value), ct);
        return decisiones.Select(d => new SubjectDecisionDto(d.PublicId, d.Outcome, d.Reason, d.DecidedAt, d.EsColegiada,
            d.DecidedByUserId is { } u ? usuarios.GetValueOrDefault(u)?.Name : null)).ToList();
    }

    /// <summary>El punto del acta que aprobó el asunto, si lo decidió un órgano (para <c>sessionItemPublicId</c> de los hechos).</summary>
    public static async Task<Guid?> PuntoDeActaAsync(IApplicationDbContext db, int? decisionId, CancellationToken ct)
    {
        if (decisionId is not { } id) return null;
        return await (from d in db.CreditDecisions.AsNoTracking()
                      join i in db.SessionItems.AsNoTracking() on d.SessionItemId equals i.Id
                      where d.Id == id
                      select (Guid?)i.PublicId).FirstOrDefaultAsync(ct);
    }
}
