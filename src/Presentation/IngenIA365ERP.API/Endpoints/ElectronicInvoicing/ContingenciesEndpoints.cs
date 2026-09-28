using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.ElectronicInvoicing;

/// <summary>
/// Las contingencias ante la DIAN (feature 012, I4, T747; contracts/api.md §24.6; contracts/dian.md §7): la bitácora de eventos 03 y 04 con
/// sus documentos pendientes, transmitidos y rechazados (<c>GET /contingencies?isOpen=&amp;type=&amp;from=&amp;to=</c>) y el detalle con los
/// documentos y la evidencia (<c>GET /contingencies/{id}</c>) con <c>ElectronicInvoicing.Contingencies.View</c>; declarar una 03
/// (<c>POST /contingencies</c> → 201; la 04 la declara sólo el canal, <c>Contingency.Dian04OnlyByChannel</c>) y cerrarla con motivo, lo que
/// fija el plazo de transmisión (<c>POST /contingencies/{id}/close</c>) con <c>Contingencies.Declare</c>. Las constancias y evidencias se suben
/// por las rutas de adjuntos de la feature 011 con dueño <c>DianContingencyEvent</c> (la regla vive en <c>AdjuntosDeModulo</c>). Toda escritura
/// con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ContingenciesEndpoints : ICarterModule
{
    public const string Ruta = "/api/electronic-invoicing/contingencies";
    private const string Ver = "ElectronicInvoicing.Contingencies.View";
    private const string Declarar = "ElectronicInvoicing.Contingencies.Declare";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Electronic Invoicing Contingencies")
            .RequireAuthorization();

        group.MapGet("/", async (bool? isOpen, ContingencyType? type, DateOnly? from, DateOnly? to, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListContingenciesQuery(isOpen, type, from, to), ct))
            .WithName("ElectronicInvoicing_Contingencies_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetContingencyQuery(id), ct))
            .WithName("ElectronicInvoicing_Contingencies_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapPost("/", async (DeclararRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new OpenContingencyCommand(body.Type ?? ContingencyType.Issuer03, body.Reason ?? string.Empty, body.StartedAt,
                    body.EvidenceNote) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{result.Value.ContingencyPublicId}", result.Value) : result;
            })
            .WithName("ElectronicInvoicing_Contingencies_Open")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Declarar);

        group.MapPost("/{id:guid}/close", async (Guid id, CerrarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseContingencyCommand(id, body.Reason ?? string.Empty, body.EndedAt, body.EvidenceNote)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("ElectronicInvoicing_Contingencies_Close")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Declarar);
    }

    /// <summary><c>POST /contingencies</c>: el tipo (sólo <c>Issuer03</c> se declara a mano), el motivo, desde cuándo y una nota de evidencia.</summary>
    public sealed record DeclararRequest(ContingencyType? Type, string? Reason, DateTime? StartedAt, string? EvidenceNote);

    /// <summary><c>POST /contingencies/{id}/close</c>: el motivo, hasta cuándo y una nota de evidencia.</summary>
    public sealed record CerrarRequest(string? Reason, DateTime? EndedAt, string? EvidenceNote);
}
