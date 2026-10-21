using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las decisiones individuales y «Mis pendientes» (feature 013, US2, T0408; contracts/api.md §4.3, FR-038, FR-042, T63, T65):
/// <c>/pending-decisions</c> (lo que quien pregunta puede decidir ahora y lo que está en agenda de sus órganos) y la decisión de una
/// solicitud o de un cupo por el mismo <see cref="RecordCreditDecisionCommand"/>. La ruta exige <b>ver</b> el sujeto
/// (<c>Lending.Applications.Read</c>, <c>Lending.CreditLimits.Read</c>); el permiso del nivel efectivo lo valida el handler y sin él responde
/// el 404 del sujeto (§1.3). Un nivel colegiado no se decide aquí (<c>Lending.Attribution.LevelRequired</c>: va a sesión). Con
/// <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DecisionsEndpoints : ICarterModule
{
    public const string VerSolicitudes = ApplicationsEndpoints.Leer;
    public const string VerCupos = CreditLimitsEndpoints.Leer;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var decisiones = LendingGroup.Grupo(app, string.Empty, "Cartera Decisiones");

        decisiones.MapGet("/pending-decisions", async (string? subject, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPendingLendingDecisionsQuery(subject, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Decisions_Pending")
            .RequirePermission(VerSolicitudes);

        decisiones.MapPost("/applications/{id:guid}/decisions", async (Guid id, DecisionRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(Comando(LendingDecisionSubjects.CreditApplication, id, body, http), ct),
                    $"/api/lending/applications/{id}"))
            .WithName("Lending_Decisions_Application")
            .ConClaveDeOperacion()
            .RequirePermission(VerSolicitudes);

        decisiones.MapPost("/credit-limits/{id:guid}/decisions", async (Guid id, DecisionRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(Comando(LendingDecisionSubjects.CreditLimit, id, body, http), ct),
                    d => $"/api/lending/credit-limits/{d.ResultingSubjectPublicId ?? id}"))
            .WithName("Lending_Decisions_CreditLimit")
            .ConClaveDeOperacion()
            .RequirePermission(VerCupos);
    }

    private static RecordCreditDecisionCommand Comando(string sujeto, Guid id, DecisionRequest body, HttpContext http) =>
        new(sujeto, id, body.Outcome, body.Reason, body.Conditions, body.ExpectedContentSha256) { OperationKey = http.ClaveDeOperacion() };

    /// <summary>
    /// <c>DecisionInput</c> (§4.3): el resultado, el motivo (obligatorio en <c>Deny</c>, <c>Return</c> y toda excepción), las condiciones
    /// menores y la huella del enrutamiento que vio quien decide.
    /// </summary>
    public sealed record DecisionRequest(
        CreditDecisionOutcome Outcome,
        string? Reason,
        DecisionConditionsInput? Conditions,
        string? ExpectedContentSha256);
}
