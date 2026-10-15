using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las novedades de crédito (feature 013, K3, US11, T0890; contracts/api.md §11.1, FR-090–FR-093): la lista y el detalle
/// (<c>Lending.Novelties.Read</c>), pedirla, simularla, radicarla, registrar consentimientos y anularla (<c>Lending.Novelties.Request</c>), decidirla
/// por el mismo <see cref="RecordCreditDecisionCommand"/> de §4.3 (sujeto <c>Novelty</c>; la ruta exige verla y el handler el permiso del nivel) y
/// aplicarla (<c>Lending.Novelties.Apply</c>). Toda escritura lleva <c>Idempotency-Key</c>; la simulación es una consulta por POST y va sin clave.
/// Fuera del alcance de oficina o línea, el mismo 404 que si la novedad no existiera (§1.3). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class NoveltiesEndpoints : ICarterModule
{
    public const string Leer = "Lending.Novelties.Read";
    public const string Pedir = "Lending.Novelties.Request";
    public const string Aplicar = "Lending.Novelties.Apply";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var novedades = LendingGroup.Grupo(app, "/novelties", "Cartera Novedades");

        novedades.MapGet("/", async (LoanNoveltyStatus? status, NoveltyCause? cause, Guid? loanPublicId, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListLoanNoveltiesQuery(status, cause, loanPublicId, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Novelties_List")
            .RequirePermission(Leer);

        novedades.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLoanNoveltyQuery(id), ct))
            .WithName("Lending_Novelties_Get")
            .RequirePermission(Leer);

        novedades.MapPost("/", async (NovedadRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RequestLoanNoveltyCommand(
                    body.LoanPublicIds ?? [], body.Mechanism, MotivosDeNovedad.Leer(body.RequestMotive), body.NewTerms ?? new NewTermsInput(),
                    body.Reason ?? string.Empty, body.InsolvencyProceedingPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), n => $"/api/lending/novelties/{n.PublicId}"))
            .WithName("Lending_Novelties_Request")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);

        novedades.MapPost("/{id:guid}/simulate", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateLoanNoveltyQuery(id), ct))
            .WithName("Lending_Novelties_Simulate")
            .RequirePermission(Pedir);

        novedades.MapPost("/{id:guid}/submit", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SubmitLoanNoveltyCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Novelties_Submit")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);

        novedades.MapPost("/{id:guid}/consents", async (Guid id, ConsentimientoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterNoveltyConsentCommand(id, body.PartyPersonPublicId, body.Consented,
                    body.AttachmentPublicId, body.AddendumAttachmentPublicId, body.CollateralPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/novelties/{id}"))
            .WithName("Lending_Novelties_Consent")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);

        novedades.MapPost("/{id:guid}/decisions", async (Guid id, DecisionsEndpoints.DecisionRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RecordCreditDecisionCommand(LendingDecisionSubjects.Novelty, id, body.Outcome, body.Reason,
                    body.Conditions, body.ExpectedContentSha256) { OperationKey = http.ClaveDeOperacion() }, ct), $"/api/lending/novelties/{id}"))
            .WithName("Lending_Novelties_Decide")
            .ConClaveDeOperacion()
            .RequirePermission(Leer);

        novedades.MapPost("/{id:guid}/apply", async (Guid id, AplicarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyLoanNoveltyCommand(id, body?.WithoutConsent, body?.AcknowledgeWarnings ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Novelties_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(Aplicar);

        novedades.MapPost("/{id:guid}/cancel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CancelLoanNoveltyCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Lending_Novelties_Cancel")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);
    }

    /// <summary>
    /// La novedad (§11.1): los créditos, el mecanismo, el motivo del deudor (<c>Hardship</c> o <c>DebtorRequest</c>, por defecto el segundo: la causa
    /// la calcula el sistema), las condiciones nuevas, el proceso de insolvencia si es su acuerdo y el motivo.
    /// </summary>
    public sealed record NovedadRequest(
        IReadOnlyList<Guid>? LoanPublicIds,
        NoveltyMechanism Mechanism,
        NoveltyRequestMotive? RequestMotive,
        NewTermsInput? NewTerms,
        Guid? InsolvencyProceedingPublicId,
        string? Reason);

    /// <summary>El consentimiento de una parte: aceptar exige el adjunto firmado <c>LendingNoveltyConsent</c>; el otrosí o pagaré nuevo es opcional.</summary>
    public sealed record ConsentimientoRequest(
        Guid PartyPersonPublicId,
        bool Consented,
        Guid? AttachmentPublicId,
        Guid? AddendumAttachmentPublicId,
        Guid? CollateralPublicId);

    /// <summary>Lo que decide quien aplica ante las partes que no consintieron, y el reconocimiento de los avisos.</summary>
    public sealed record AplicarRequest(IReadOnlyList<WithoutConsentInput>? WithoutConsent, bool? AcknowledgeWarnings);

    /// <summary>El motivo de la anulación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
