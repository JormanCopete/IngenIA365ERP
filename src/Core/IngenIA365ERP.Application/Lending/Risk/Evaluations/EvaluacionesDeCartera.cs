using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk.Evaluations;

/// <summary>Un crédito que se propone recalificar en la evaluación (api.md §12.6, <c>loans[]</c>). (nuevo)</summary>
public sealed record PortfolioEvaluationLoanInput(
    Guid LoanPublicId,
    RatingCategory ProposedCategory,
    string Reason,
    RatingSubcategory? ProposedSubcategory = null,
    string? CriteriaScoresJson = null,
    Guid? SupportAttachmentPublicId = null);

/// <summary>Un crédito evaluado con su calificación por las reglas al corte, la propuesta y la justificación. (nuevo)</summary>
public sealed record PortfolioEvaluationLoanDto(
    Guid LoanPublicId,
    string LoanNumber,
    RefDto Debtor,
    RatingCategory RuleRatingAtCutoff,
    RatingCategory ProposedCategory,
    RatingSubcategory? ProposedSubcategory,
    string Reason,
    string? CriteriaScoresJson,
    Guid? SupportAttachmentPublicId);

/// <summary>Un punto de acta de la evaluación: la sesión, su número, el orden del punto, si recomienda o decide y su resultado. (nuevo)</summary>
public sealed record PortfolioEvaluationSessionItemDto(Guid ItemPublicId, Guid SessionPublicId, string? MinutesNumber, int Order, SessionItemKind Kind,
    CreditDecisionOutcome? Outcome);

/// <summary>
/// Una evaluación de cartera (api.md §12.6): lista (sin créditos) y detalle (con la página de créditos pedida). <c>Warnings</c> (bloque D) trae, al
/// prepararla, lo de la norma que sigue pendiente de validar (<see cref="NormaPendienteDeCartera"/>): no bloquea. (nuevo)
/// </summary>
public sealed record PortfolioEvaluationDto(
    Guid PublicId,
    string Number,
    DateOnly CutoffDate,
    string EffectMonth,
    PortfolioEvaluationStatus Status,
    string CriteriaJson,
    string PreparedBy,
    DateTime PreparedAt,
    int LoanCount,
    PortfolioEvaluationSessionItemDto? RecommendationItem,
    PortfolioEvaluationSessionItemDto? ApprovalItem,
    string? AppliedBy,
    DateTime? AppliedAt,
    IReadOnlyList<PortfolioEvaluationLoanDto> Loans,
    int Page,
    int PageSize,
    IReadOnlyList<AvisoDeNormaPendienteDto>? Warnings = null);

/// <summary>
/// Lo común de la evaluación de cartera (feature 013, K3, US14, T0912; FR-102; data-model §23.7): el expediente, el prefijo, los códigos y mensajes, los
/// órganos que la tratan (el comité de riesgos la recomienda, el consejo la aprueba), la recomendación cerrada de un punto <c>Recommendation</c> y la
/// respuesta. (nuevo)
/// </summary>
public static class EvaluacionesDeCartera
{
    /// <summary>El tipo de expediente de una evaluación en <c>LND_CaseParticipations</c>.</summary>
    public const string Expediente = "PortfolioEvaluation";

    public const string Prefijo = "EVC-";

    public const string CodigoInexistente = "Lending.PortfolioEvaluation.NotFound";
    public const string CodigoSuperpuesta = "Lending.PortfolioEvaluation.Overlaps";
    public const string CodigoNoBorrador = PortfolioEvaluation.CodigoNoBorrador;
    public const string CodigoEstadoInvalido = PortfolioEvaluation.CodigoEstadoInvalido;
    /// <summary>
    /// El aviso de una evaluación fuera de la periodicidad del tipo de entidad (decisión del dueño del 2026-10-18: informativo; hasta esa fecha era el
    /// error <c>Lending.PortfolioEvaluation.PeriodInvalid</c>).
    /// </summary>
    public const string AvisoFueraDePeriodicidad = "PortfolioEvaluationOutsidePeriodicity";
    public const string CodigoCreditoNoVivo = "Lending.PortfolioEvaluation.LoanNotLive";
    public const string CodigoSinComite = "Lending.PortfolioEvaluation.RiskCommitteeMissing";
    public const string CodigoSinRecomendacion = "Lending.PortfolioEvaluation.RecommendationRequired";

    public static Error Inexistente() => new(CodigoInexistente, "La evaluación de cartera no existe.");

    public static Error Superpuesta(DateOnly corte, string numero) => new ErrorConDatos(CodigoSuperpuesta,
        $"Ya hay una evaluación de cartera sin aplicar con corte {corte:yyyy-MM-dd} ({numero}): termínela antes de preparar otra del mismo corte.",
        new { cutoffDate = corte, number = numero });

    public static AvisoDeNormaPendienteDto FueraDePeriodicidad(string tipo, IReadOnlyList<int> meses, int mesesHastaElEfecto, DateOnly efectoEsperado) =>
        new(AvisoFueraDePeriodicidad,
            $"El tipo de entidad ({tipo}) evalúa con corte en los meses {string.Join(", ", meses)} y la recalificación se refleja {mesesHastaElEfecto} "
            + $"mes(es) después del corte (para este corte, {efectoEsperado:yyyy-MM}). La evaluación se prepara igual: la periodicidad es informativa y la "
            + "entidad decide según su calendario.", null);

    public static Error CreditoNoVivo(string numero, DateOnly corte) => new ErrorConDatos(CodigoCreditoNoVivo,
        $"El crédito {numero} no estaba vigente al corte {corte:yyyy-MM-dd}: la evaluación recalifica sólo la cartera viva al corte.",
        new { loanNumber = numero, cutoffDate = corte });

    public static Error SinComite() => new(CodigoSinComite,
        "La cooperativa no tiene un comité de riesgos activo entre sus órganos: la evaluación de cartera la recomienda ese comité. Regístrelo en Órganos.");

    public static Error SinRecomendacion() => new(CodigoSinRecomendacion,
        "La evaluación todavía no tiene la recomendación del comité de riesgos con acta cerrada: llévela primero a una sesión del comité.");

    /// <summary>El órgano activo de la clase dada (comité de riesgos o consejo), o nulo.</summary>
    public static Task<int?> OrganoAsync(IApplicationDbContext db, GoverningBodyKind clase, CancellationToken ct) =>
        db.GoverningBodies.AsNoTracking().Where(b => b.Kind == clase && b.IsActive && !b.IsDeleted).OrderBy(b => b.Id).Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// La última recomendación favorable del comité de riesgos sobre la evaluación en un punto <c>Recommendation</c> de una sesión cerrada (el cierre no
    /// llama al asunto por esos puntos: la recoge el consejo al decidir). Nula si no hay.
    /// </summary>
    public static Task<int?> RecomendacionCerradaAsync(IApplicationDbContext db, Guid evaluacion, CancellationToken ct) =>
        (from i in db.SessionItems.AsNoTracking()
         join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
         join b in db.GoverningBodies.AsNoTracking() on s.GoverningBodyId equals b.Id
         where !i.IsDeleted && i.SubjectType == LendingDecisionSubjects.PortfolioEvaluation && i.SubjectPublicId == evaluacion
               && i.Kind == SessionItemKind.Recommendation && s.Status == SessionStatus.Closed && b.Kind == GoverningBodyKind.RiskCommittee
               && (i.Outcome == CreditDecisionOutcome.Approve || i.Outcome == CreditDecisionOutcome.ApproveWithConditions)
         orderby s.ClosedAt descending, i.Id descending
         select (int?)i.Id).FirstOrDefaultAsync(ct);

    /// <summary>La evaluación si existe; si no, <c>Lending.PortfolioEvaluation.NotFound</c>.</summary>
    public static async Task<Result<PortfolioEvaluation>> LeerAsync(IApplicationDbContext db, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.PortfolioEvaluations : db.PortfolioEvaluations.AsNoTracking();
        var e = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        return e is null ? Result.Failure<PortfolioEvaluation>(Inexistente()) : Result.Success(e);
    }

    public static string MesDeEfecto(PortfolioEvaluation e) => $"{e.EffectiveYear:D4}-{e.EffectiveMonth:D2}";

    /// <summary>La respuesta, con los créditos de <paramref name="lineas"/> (ya filtradas por el alcance y paginadas por quien llama).</summary>
    public static async Task<PortfolioEvaluationDto> DtoAsync(IApplicationDbContext db, PortfolioEvaluation e, IReadOnlyList<PortfolioEvaluationLoan> lineas,
        int pagina, int tamano, CancellationToken ct)
    {
        var ids = lineas.Select(l => l.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        var (personas, _, _) = await ListLoansQueryHandler.ReferenciasAsync(db, creditos, ct);
        var porId = creditos.ToDictionary(c => c.Id);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, new[] { e.PreparedByUserId, e.AppliedByUserId ?? 0 }.Where(u => u > 0), ct);
        var puntos = new[] { e.RecommendationSessionItemId, e.ApprovalSessionItemId }.Where(p => p is not null).Select(p => p!.Value).ToList();
        var dePuntos = await (from i in db.SessionItems.AsNoTracking()
                              join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                              where puntos.Contains(i.Id)
                              select new { i.Id, Dto = new PortfolioEvaluationSessionItemDto(i.PublicId, s.PublicId, s.Number, i.Order, i.Kind, i.Outcome) })
            .ToDictionaryAsync(x => x.Id, x => x.Dto, ct);
        var lineasDto = lineas.Where(l => porId.ContainsKey(l.LoanId)).OrderBy(l => porId[l.LoanId].Number, StringComparer.Ordinal)
            .Select(l => new PortfolioEvaluationLoanDto(porId[l.LoanId].PublicId, porId[l.LoanId].Number,
                personas.TryGetValue(porId[l.LoanId].DebtorPersonId, out var p) ? p : new RefDto(Guid.Empty, string.Empty), l.RuleRatingAtCutoff,
                l.ProposedRating, l.ProposedSubcategory, l.Justification, l.CriteriaScoresJson, l.SupportAttachmentPublicId))
            .ToList();
        return new PortfolioEvaluationDto(e.PublicId, e.Number, e.CutoffDate, MesDeEfecto(e), e.Status, e.CriteriaJson,
            usuarios.GetValueOrDefault(e.PreparedByUserId)?.Name ?? string.Empty, e.PreparedAt, e.LoanCount,
            e.RecommendationSessionItemId is { } r ? dePuntos.GetValueOrDefault(r) : null, e.ApprovalSessionItemId is { } a ? dePuntos.GetValueOrDefault(a) : null,
            e.AppliedByUserId is { } u ? usuarios.GetValueOrDefault(u)?.Name : null, e.AppliedAt, lineasDto, pagina, tamano);
    }
}
