using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk.Evaluations;

/// <summary>
/// El asunto de acta <c>PortfolioEvaluation</c> (feature 013, K3, US14, T0912; FR-102): la evaluación de cartera que <b>recomienda el comité de riesgos</b>
/// y <b>aprueba el consejo</b>, cada uno con su punto. Pendiente en borrador o recomendada; quien la preparó no vota sus puntos; la huella cubre el corte,
/// el mes de efecto, los criterios y cada crédito con su propuesta.
/// <list type="bullet">
/// <item>Mientras no haya recomendación, la decide el comité de riesgos (un punto <c>Decision</c> del consejo responde
/// <c>Lending.Session.SubjectNotForBody</c>). La recomendación puede ser un punto <c>Recommendation</c> del comité —el cierre del acta no llama al asunto
/// por él: lo recoge el consejo al decidir— o un punto <c>Decision</c> del comité, que al cerrarse la deja <c>Recommended</c> (o <c>Rejected</c>).</item>
/// <item>Recomendada (o con la recomendación cerrada del comité), la decide el consejo: la aprobación la deja <c>Approved</c> con su punto; otro
/// resultado, <c>Rejected</c>.</item>
/// </list>
/// Aplicarla es otro acto (<see cref="ApplyPortfolioEvaluationCommand"/>). (nuevo)
/// </summary>
public sealed class AsuntoDeEvaluacionDeCartera(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.PortfolioEvaluation;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var leida = await EvaluacionesDeCartera.LeerAsync(db, subjectPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<AsuntoDeActa>(leida.Error);
        var e = leida.Value;
        var recomendada = e.Status == PortfolioEvaluationStatus.Recommended
                          || (e.Status == PortfolioEvaluationStatus.Draft && await EvaluacionesDeCartera.RecomendacionCerradaAsync(db, e.PublicId, ct) is not null);
        var organo = await EvaluacionesDeCartera.OrganoAsync(db, recomendada ? GoverningBodyKind.BoardOfDirectors : GoverningBodyKind.RiskCommittee, ct);
        if (organo is null) return Result.Failure<AsuntoDeActa>(recomendada ? WriteOffs.ErroresDeCastigo.SinConsejo() : EvaluacionesDeCartera.SinComite());

        var lineas = await db.PortfolioEvaluationLoans.AsNoTracking().Where(l => l.PortfolioEvaluationId == e.Id && !l.IsDeleted).OrderBy(l => l.LoanId)
            .ToListAsync(ct);
        var ids = lineas.Select(l => l.LoanId).ToList();
        var numeros = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => new { l.Number, l.PublicId }, ct);
        var huella = new HuellaDeCartera()
            .Identificador("evaluation", e.PublicId)
            .Enumeracion("status", (PortfolioEvaluationStatus?)e.Status)
            .Fecha("cutoffDate", e.CutoffDate)
            .Texto("effectMonth", EvaluacionesDeCartera.MesDeEfecto(e))
            .Texto("criteria", e.CriteriaJson)
            .Lista("loans", lineas, (h, l) => h
                .Identificador("loan", numeros[l.LoanId].PublicId)
                .Enumeracion("ruleRating", (RatingCategory?)l.RuleRatingAtCutoff)
                .Enumeracion("proposed", (RatingCategory?)l.ProposedRating)
                .Enumeracion("subcategory", l.ProposedSubcategory)
                .Texto("justification", l.Justification))
            .Calcular();
        var empeoran = lineas.Count(l => l.ProposedRating > l.RuleRatingAtCutoff);
        var titulo = string.Create(CultureInfo.InvariantCulture,
            $"Evaluación de cartera {e.Number}, corte {e.CutoffDate:yyyy-MM-dd}: {e.LoanCount} crédito(s), efecto en el cierre de {EvaluacionesDeCartera.MesDeEfecto(e)}");
        var resumen = string.Create(CultureInfo.InvariantCulture, $"{empeoran} crédito(s) con una calificación propuesta peor que la de las reglas. ")
            + (recomendada ? "Recomendada por el comité de riesgos; la aprueba el consejo de administración." : "La recomienda el comité de riesgos.")
            + " " + string.Join("; ", lineas.Take(20).Select(l =>
                $"{numeros[l.LoanId].Number}: {l.RuleRatingAtCutoff} → {l.ProposedRating}{(l.ProposedSubcategory is { } s ? $" ({s})" : string.Empty)}, {l.Justification}"))
            + (lineas.Count > 20 ? $"; y {lineas.Count - 20} más." : ".");

        var personas = new List<PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == e.PreparedByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } preparo)
            personas.Add(new PersonaDelAsunto(preparo, MotivosDeExclusion.Radicador));
        return Result.Success(new AsuntoDeActa(titulo, resumen,
            e.Status is PortfolioEvaluationStatus.Draft or PortfolioEvaluationStatus.Recommended, e.Status.ToString(),
            new ExpedienteDeCartera(EvaluacionesDeCartera.Expediente, e.PublicId), huella)
        {
            Personas = personas,
            GoverningBodyId = organo,
        });
    }

    public async Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct)
    {
        var leida = await EvaluacionesDeCartera.LeerAsync(db, decision.Punto.SubjectPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure(leida.Error);
        var e = leida.Value;
        var aprueba = decision.Resultado is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions;
        var clase = await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == decision.Sesion.GoverningBodyId)
            .Select(b => (GoverningBodyKind?)b.Kind).FirstOrDefaultAsync(ct);
        try
        {
            switch (e.Status)
            {
                case PortfolioEvaluationStatus.Draft when clase == GoverningBodyKind.RiskCommittee:
                    // El comité decide sobre el borrador: su punto es la recomendación (o el rechazo).
                    if (aprueba) e.Recomendar(decision.Punto.Id);
                    else e.Rechazar(decision.Punto.Id);
                    break;
                case PortfolioEvaluationStatus.Draft:
                    // El consejo, con la recomendación cerrada de un punto Recommendation del comité.
                    if (await EvaluacionesDeCartera.RecomendacionCerradaAsync(db, e.PublicId, ct) is not { } recomendacion)
                        return Result.Failure(EvaluacionesDeCartera.SinRecomendacion());
                    e.Recomendar(recomendacion);
                    Decidir(e, aprueba, decision.Punto.Id);
                    break;
                case PortfolioEvaluationStatus.Recommended:
                    Decidir(e, aprueba, decision.Punto.Id);
                    break;
                default:
                    return Result.Failure(ErroresDeGobierno.SujetoNoPendiente(e.Status.ToString()));
            }
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure(ErroresDeGobierno.De(ex.Problema));
        }
        return Result.Success();
    }

    private static void Decidir(PortfolioEvaluation e, bool aprueba, int punto)
    {
        if (aprueba) e.Aprobar(punto);
        else e.Rechazar(punto);
    }
}
