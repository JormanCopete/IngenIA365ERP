using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// La copia de los valores nacionales en la cooperativa (feature 013, T37–T41; contracts/api.md §21.3): sólo lectura y
/// conformidad —la cooperativa nunca modifica la copia—. Consulta con <c>Core.NationalValues.View</c>; revisión con
/// <c>Core.NationalValues.Review</c> e <c>Idempotency-Key</c>. Con <c>date</c> (D) y <c>recordedAsOf</c> (R) el listado aplica la regla
/// bitemporal (C83). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class NationalValuesEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.NationalValues.View";
    public const string PermisoDeRevision = "Core.NationalValues.Review";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var valores = app.MapGroup("/api/core/national-values")
            .WithTags("Core Valores nacionales")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        valores.MapGet("/", async (string? code, string? key, DateOnly? date, DateTime? recordedAsOf, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListNationalValuesQuery(code, key, date, recordedAsOf, page ?? 1, pageSize ?? 50), ct))
            .WithName("Core_NationalValues_List")
            .RequirePermission(PermisoDeConsulta);

        valores.MapGet("/replications", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListNationalValueReplicationsQuery(page ?? 1, pageSize ?? 50), ct))
            .WithName("Core_NationalValues_Replications")
            .RequirePermission(PermisoDeConsulta);

        valores.MapPost("/{id:guid}/review", async (Guid id, RevisionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ReviewNationalValueCommand(id, body.Decision, body.Notes) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/national-values/{id}", new { reviewPublicId = r.Value }) : r;
            })
            .WithName("Core_NationalValues_Review")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRevision);

        var documentos = app.MapGroup("/api/core/regulatory-documents")
            .WithTags("Core Valores nacionales")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        documentos.MapGet("/", async (string? kind, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRegulatoryDocumentsQuery(kind, date), ct))
            .WithName("Core_RegulatoryDocuments_List")
            .RequirePermission(PermisoDeConsulta);

        documentos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRegulatoryDocumentQuery(id), ct))
            .WithName("Core_RegulatoryDocuments_Get")
            .RequirePermission(PermisoDeConsulta);

        documentos.MapPost("/{id:guid}/review", async (Guid id, RevisionDeDocumentoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ReviewRegulatoryDocumentCommand(id, body.Status, body.Notes) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/regulatory-documents/{id}", new { reviewPublicId = r.Value }) : r;
            })
            .WithName("Core_RegulatoryDocuments_Review")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRevision);
    }

    /// <summary>La revisión de una copia de valor (§21.3: <c>{ decision, notes }</c>).</summary>
    public sealed record RevisionRequest(NationalValueReviewDecision Decision, string? Notes);

    /// <summary>La revisión de un documento (§21.3: <c>{ status, notes }</c>).</summary>
    public sealed record RevisionDeDocumentoRequest(NationalValueReviewDecision Status, string? Notes);
}
