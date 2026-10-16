using System.Text.Json;
using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Lending.Risk.Evaluations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Riesgo y cierres de Cartera (feature 013, US9, T0712; contracts/api.md §12.1–§12.5; FR-099–FR-107, T45–T50), sobre el grupo de Cartera
/// (<see cref="LendingGroup"/>):
/// <list type="bullet">
/// <item><c>/api/lending/risk/policy</c>: la norma, la política SARC del consejo y la efectiva celda a celda (<c>Lending.Parameters.View</c>);
/// registrar una versión (<c>Lending.RiskPolicy.Manage</c>, 201) y validarla con su punto de acta (<c>Lending.RiskPolicy.Validate</c>);</item>
/// <item><c>/risk/ratings</c> (la calificación <b>informativa</b>, que no se guarda) y <c>/risk/analysis-ratings</c> (la calificación por análisis:
/// leer con <c>Lending.Ratings.Read</c>, poner y levantar con <c>Lending.Ratings.Override</c>);</item>
/// <item><c>/risk/monthly-closes</c>: la lista, la preparación y la orden del cierre del mes (<c>Lending.MonthlyClose.Execute</c>, 202: sólo encola
/// la corrida <c>MonthlyClose</c>), el encabezado, las líneas, el reporte de cambios, el cuadre con Contabilidad
/// (<c>Lending.Reconciliation.View</c>) y la reapertura (<c>Lending.MonthlyClose.Reopen</c>, 202: encola la corrida <c>CloseReopen</c>);</item>
/// <item><c>/risk/impairment-model</c> (sólo <c>GET</c>: <c>POST /elections</c> no existe hasta K6a y responde 404) y
/// <c>/risk/arrears-history/completeness</c>;</item>
/// <item><c>GET /api/lending/loans/{id}/rating</c>: la informativa del crédito con sus candidatas, la oficial del último cierre y el análisis.</item>
/// </list>
/// Toda escritura lleva <c>Idempotency-Key</c> (<c>LosComandosDeCarteraLlevanClave</c>); cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class RiskEndpoints : ICarterModule
{
    public const string VerParametros = "Lending.Parameters.View";
    public const string RegistrarPolitica = "Lending.RiskPolicy.Manage";
    public const string ValidarPolitica = "Lending.RiskPolicy.Validate";
    public const string LeerCalificaciones = "Lending.Ratings.Read";
    public const string CalificarPorAnalisis = "Lending.Ratings.Override";
    public const string Cerrar = "Lending.MonthlyClose.Execute";
    public const string Reabrir = "Lending.MonthlyClose.Reopen";
    public const string VerConciliacion = "Lending.Reconciliation.View";
    public const string PrepararEvaluacion = "Lending.PortfolioEvaluations.Prepare";
    public const string AplicarEvaluacion = "Lending.PortfolioEvaluations.Apply";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var riesgo = LendingGroup.Grupo(app, "/risk", "Cartera Riesgo y cierres");

        // ------------------------------------------------------------------------------------------ política SARC (§12.1) --
        riesgo.MapGet("/policy", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRiskPolicyQuery(date), ct))
            .WithName("Lending_Risk_Policy_Get")
            .RequirePermission(VerParametros);

        riesgo.MapPost("/policy/versions", async (VersionDePoliticaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddRiskPolicyVersionCommand(body.ValidFrom, Texto(body.Overrides), body.Reason ?? string.Empty)
                    { OperationKey = http.ClaveDeOperacion() }, ct), v => $"{LendingGroup.Prefijo}/risk/policy?date={v.ValidFrom:yyyy-MM-dd}"))
            .WithName("Lending_Risk_Policy_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(RegistrarPolitica);

        riesgo.MapPost("/policy/versions/{id:guid}/validate", async (Guid id, ValidacionDePoliticaRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ValidateRiskPolicyVersionCommand(id, body.SessionItemPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Risk_Policy_Validate")
            .ConClaveDeOperacion()
            .RequirePermission(ValidarPolitica);

        // ---------------------------------------------------------------------- calificación informativa y análisis (§12.2) --
        riesgo.MapGet("/ratings", async (DateOnly? date, DateTime? recordedAsOf, Guid? line, Guid? office, RatingCategory? category, RatingCause? cause,
                int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDailyRatingsQuery(date, recordedAsOf, line, office, category, cause, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Risk_Ratings_List")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/analysis-ratings", async (bool? active, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAnalysisRatingsQuery(active, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Risk_AnalysisRatings_List")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapPost("/analysis-ratings", async (CalificacionPorAnalisisRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SetAnalysisRatingCommand(body.LoanPublicId, body.Category, body.Subcategory, body.Reason ?? string.Empty,
                    body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct), a => $"{LendingGroup.Prefijo}/risk/analysis-ratings/{a.PublicId}"))
            .WithName("Lending_Risk_AnalysisRatings_Set")
            .ConClaveDeOperacion()
            .RequirePermission(CalificarPorAnalisis);

        riesgo.MapPost("/analysis-ratings/{id:guid}/lift", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new LiftAnalysisRatingCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Lending_Risk_AnalysisRatings_Lift")
            .ConClaveDeOperacion()
            .RequirePermission(CalificarPorAnalisis);

        // ------------------------------------------------------------------------------------------- cierre mensual (§12.3) --
        riesgo.MapGet("/monthly-closes", async (int? year, MonthlyCloseKind? kind, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMonthlyClosesQuery(year, kind), ct))
            .WithName("Lending_Risk_MonthlyCloses_List")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/monthly-closes/{year:int}/{month:int}/readiness", async (int year, int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMonthlyCloseReadinessQuery(year, month), ct))
            .WithName("Lending_Risk_MonthlyCloses_Readiness")
            .RequirePermission(Cerrar);

        riesgo.MapPost("/monthly-closes/{year:int}/{month:int}/close", async (int year, int month, OrdenDeCierreRequest? body, HttpContext http, ISender sender,
                CancellationToken ct) =>
            {
                var r = await sender.Send(new OrderMonthlyCloseCommand(year, month, body?.Reason) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Accepted($"{LendingGroup.Prefijo}/process-runs/{r.Value.ProcessRunPublicId}", r.Value) : r;
            })
            .WithName("Lending_Risk_MonthlyCloses_Close")
            .ConClaveDeOperacion()
            .RequirePermission(Cerrar);

        riesgo.MapGet("/monthly-closes/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMonthlyCloseQuery(id), ct))
            .WithName("Lending_Risk_MonthlyCloses_Get")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/monthly-closes/{id:guid}/loans", async (Guid id, RatingCategory? category, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMonthlyCloseLoansQuery(id, category, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Risk_MonthlyCloses_Loans")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/monthly-closes/{id:guid}/changes", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMonthlyCloseChangesQuery(id, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Risk_MonthlyCloses_Changes")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/monthly-closes/{id:guid}/reconciliation", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMonthlyCloseReconciliationQuery(id), ct))
            .WithName("Lending_Risk_MonthlyCloses_Reconciliation")
            .RequirePermission(VerConciliacion);

        riesgo.MapPost("/monthly-closes/{id:guid}/reopen", async (Guid id, ReaperturaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ReopenMonthlyCloseCommand(id, body.Reason ?? string.Empty, body.SessionItemPublicId)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Accepted($"{LendingGroup.Prefijo}/process-runs/{r.Value.ProcessRunPublicId}", r.Value) : r;
            })
            .WithName("Lending_Risk_MonthlyCloses_Reopen")
            .ConClaveDeOperacion()
            .RequirePermission(Reabrir);

        // ------------------------------------------------------------- modelo por segmento e historial de mora (§12.4, §12.5) --
        riesgo.MapGet("/impairment-model", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetImpairmentModelBySegmentQuery(date), ct))
            .WithName("Lending_Risk_ImpairmentModel_Get")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/arrears-history/completeness", async (DateOnly? date, string? segment, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetArrearsHistoryCompletenessQuery(date, segment), ct))
            .WithName("Lending_Risk_ArrearsHistory_Completeness")
            .RequirePermission(LeerCalificaciones);

        // ------------------------------------------------------------------------------- evaluación de cartera (§12.6, K3) --
        // La recomienda el comité de riesgos y la aprueba el consejo por sus puntos de acta (AsuntoDeEvaluacionDeCartera); aquí sólo se lee,
        // se prepara y se aplica con esos dos puntos.
        riesgo.MapGet("/evaluations", async (PortfolioEvaluationStatus? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPortfolioEvaluationsQuery(status), ct))
            .WithName("Lending_Risk_Evaluations_List")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapGet("/evaluations/{id:guid}", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPortfolioEvaluationQuery(id, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Risk_Evaluations_Get")
            .RequirePermission(LeerCalificaciones);

        riesgo.MapPost("/evaluations", async (EvaluacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new PreparePortfolioEvaluationCommand(body.CutoffDate, body.EffectMonth ?? string.Empty,
                    Criterios(body.Criteria), body.Loans ?? []) { OperationKey = http.ClaveDeOperacion() }, ct),
                    e => $"{LendingGroup.Prefijo}/risk/evaluations/{e.PublicId}"))
            .WithName("Lending_Risk_Evaluations_Prepare")
            .ConClaveDeOperacion()
            .RequirePermission(PrepararEvaluacion);

        // Bloque D (decisión del dueño del 2026-10-15): descartar la evaluación mientras no se aplique, con motivo (va a la auditoría).
        riesgo.MapPost("/evaluations/{id:guid}/discard", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardPortfolioEvaluationCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Risk_Evaluations_Discard")
            .ConClaveDeOperacion()
            .RequirePermission(PrepararEvaluacion);

        riesgo.MapPost("/evaluations/{id:guid}/apply", async (Guid id, AplicarEvaluacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyPortfolioEvaluationCommand(id, body.RecommendationSessionItemPublicId, body.ApprovalSessionItemPublicId)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Risk_Evaluations_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(AplicarEvaluacion);

        // --------------------------------------------------------------------------------- la calificación de un crédito --
        var creditos = LendingGroup.Grupo(app, "/loans", "Cartera Riesgo y cierres");
        creditos.MapGet("/{id:guid}/rating", async (Guid id, DateOnly? date, DateTime? recordedAsOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLoanRatingQuery(id, date, recordedAsOf), ct))
            .WithName("Lending_Loans_Rating")
            .RequirePermission(LeerCalificaciones);
    }

    /// <summary><c>{ cutoffDate, effectMonth (AAAA-MM), criteria (objeto JSON o su texto), loans: [{ loanPublicId, proposedCategory, reason, … }] }</c> (§12.6).</summary>
    public sealed record EvaluacionRequest(DateOnly CutoffDate, string? EffectMonth, JsonElement? Criteria, IReadOnlyList<PortfolioEvaluationLoanInput>? Loans);

    /// <summary><c>{ recommendationSessionItemPublicId, approvalSessionItemPublicId }</c> (§12.6).</summary>
    public sealed record AplicarEvaluacionRequest(Guid RecommendationSessionItemPublicId, Guid ApprovalSessionItemPublicId);

    /// <summary>Los criterios llegan como objeto JSON o como su texto; ausentes, nulos.</summary>
    private static string? Criterios(JsonElement? criterios) => criterios switch
    {
        null => null,
        { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        { ValueKind: JsonValueKind.String } c => c.GetString(),
        { } c => c.GetRawText(),
    };

    /// <summary><c>{ validFrom, overrides (esquema parcial del Anexo 1: el objeto o su texto JSON), reason }</c> (§12.1).</summary>
    public sealed record VersionDePoliticaRequest(DateOnly ValidFrom, JsonElement? Overrides, string? Reason);

    /// <summary>La política llega como objeto JSON (o como su texto, desde un formulario); el comando la recibe como texto y la canoniza.</summary>
    private static string Texto(JsonElement? politica) => politica switch
    {
        null => string.Empty,
        { ValueKind: JsonValueKind.String } p => p.GetString() ?? string.Empty,
        { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => string.Empty,
        { } p => p.GetRawText(),
    };

    /// <summary><c>{ sessionItemPublicId }</c>: el punto del acta del consejo que la aprobó (C52).</summary>
    public sealed record ValidacionDePoliticaRequest(Guid SessionItemPublicId);

    /// <summary><c>{ loanPublicId, category, subcategory?, reason, supportAttachmentPublicId? }</c> (§12.2).</summary>
    public sealed record CalificacionPorAnalisisRequest(Guid LoanPublicId, RatingCategory Category, RatingSubcategory? Subcategory, string? Reason,
        Guid? SupportAttachmentPublicId);

    /// <summary>El motivo de levantar la calificación por análisis.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary><c>{ reason? }</c> de la orden del cierre.</summary>
    public sealed record OrdenDeCierreRequest(string? Reason);

    /// <summary><c>{ reason, sessionItemPublicId? }</c> de la reapertura (§12.3).</summary>
    public sealed record ReaperturaRequest(string? Reason, Guid? SessionItemPublicId);
}
