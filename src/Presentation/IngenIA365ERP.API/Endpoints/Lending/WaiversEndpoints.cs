using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Waivers;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las condonaciones (feature 013, K3, US11, T0890; contracts/api.md §11.2, FR-094): la lista y el detalle (<c>Lending.Loans.Read</c>), pedirla y
/// anularla (<c>Lending.Condonations.Request</c>), decidirla por <see cref="RecordCreditDecisionCommand"/> (sujeto <c>Condonation</c>; la ruta exige
/// ver el crédito y el handler el permiso del nivel) y aplicarla (<c>Lending.Condonations.Apply</c>). Toda escritura lleva <c>Idempotency-Key</c>.
/// Fuera del alcance, el mismo 404 (§1.3). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class WaiversEndpoints : ICarterModule
{
    public const string Leer = "Lending.Loans.Read";
    public const string Pedir = "Lending.Condonations.Request";
    public const string Aplicar = "Lending.Condonations.Apply";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var condonaciones = LendingGroup.Grupo(app, "/waivers", "Cartera Condonaciones");

        condonaciones.MapGet("/", async (WaiverStatus? status, Guid? loanPublicId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListWaiversQuery(status, loanPublicId, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Waivers_List")
            .RequirePermission(Leer);

        condonaciones.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetWaiverQuery(id), ct))
            .WithName("Lending_Waivers_Get")
            .RequirePermission(Leer);

        condonaciones.MapPost("/", async (CondonacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RequestWaiverCommand(body.LoanPublicId, body.Concepts ?? [], body.Reason ?? string.Empty,
                    body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct), w => $"/api/lending/waivers/{w.PublicId}"))
            .WithName("Lending_Waivers_Request")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);

        condonaciones.MapPost("/{id:guid}/decisions", async (Guid id, DecisionsEndpoints.DecisionRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RecordCreditDecisionCommand(LendingDecisionSubjects.Condonation, id, body.Outcome, body.Reason,
                    body.Conditions, body.ExpectedContentSha256) { OperationKey = http.ClaveDeOperacion() }, ct), $"/api/lending/waivers/{id}"))
            .WithName("Lending_Waivers_Decide")
            .ConClaveDeOperacion()
            .RequirePermission(Leer);

        condonaciones.MapPost("/{id:guid}/apply", async (Guid id, AplicarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyWaiverCommand(id, body?.AcknowledgeWarnings ?? false) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Waivers_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(Aplicar);

        condonaciones.MapPost("/{id:guid}/cancel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CancelWaiverCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Lending_Waivers_Cancel")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);
    }

    /// <summary>La condonación (§11.2): el crédito, los conceptos con su monto (y la cuota, si se fija), el motivo y el soporte.</summary>
    public sealed record CondonacionRequest(Guid LoanPublicId, IReadOnlyList<WaiverConceptInput>? Concepts, string? Reason, Guid? SupportAttachmentPublicId);

    /// <summary>El reconocimiento de los avisos al aplicar.</summary>
    public sealed record AplicarRequest(bool? AcknowledgeWarnings);

    /// <summary>El motivo de la anulación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
