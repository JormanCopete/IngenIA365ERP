using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Adapters;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las consultas a centrales de riesgo (feature 013, US2, T0408; contracts/api.md §4.4, FR-036, T69, T71, T73): consultar
/// (<c>Lending.CreditBureau.Query</c>, con autorización del titular y <c>Idempotency-Key</c>; la respuesta trae el estado, nunca el
/// contenido), la lista, el detalle con el informe y el enlace firmado de 60 s al adjunto <c>LendingBureauReport</c>
/// (<c>.ReadReports</c>; los dos últimos quedan auditados como <c>Lending.CreditBureauReport.Viewed</c>). El <c>download-link</c> no lleva clave:
/// repetirlo emite otro enlace (§1.5). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class BureauQueriesEndpoints : ICarterModule
{
    public const string Consultar = "Lending.CreditBureau.Query";
    public const string LeerInformes = "Lending.CreditBureau.ReadReports";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var centrales = LendingGroup.Grupo(app, "/bureau-queries", "Cartera Centrales");

        centrales.MapGet("/", async (Guid? personPublicId, Guid? applicationPublicId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBureauQueriesQuery(personPublicId, applicationPublicId, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_BureauQueries_List")
            .RequirePermission(LeerInformes);

        centrales.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetBureauQueryQuery(id), ct))
            .WithName("Lending_BureauQueries_Get")
            .RequirePermission(LeerInformes);

        centrales.MapPost("/", async (ConsultaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new QueryCreditBureauCommand(
                    body.PersonPublicId, body.BureauCode ?? string.Empty, body.Purpose ?? string.Empty, body.ApplicationPublicId, body.CreditLimitPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), c => $"/api/lending/bureau-queries/{c.BureauQueryPublicId}"))
            .WithName("Lending_BureauQueries_Query")
            .ConClaveDeOperacion()
            .RequirePermission(Consultar);

        centrales.MapPost("/{id:guid}/download-link", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IssueBureauReportDownloadLinkCommand(id), ct))
            .WithName("Lending_BureauQueries_DownloadLink")
            .RequireRateLimiting(LimiteDeAdjuntos.Politica)
            .RequirePermission(LeerInformes);
    }

    /// <summary>La consulta (§4.4): la persona, la central por su código, la finalidad y el expediente que la pide.</summary>
    public sealed record ConsultaRequest(
        Guid PersonPublicId,
        string? BureauCode,
        string? Purpose,
        Guid? ApplicationPublicId,
        Guid? CreditLimitPublicId);
}
