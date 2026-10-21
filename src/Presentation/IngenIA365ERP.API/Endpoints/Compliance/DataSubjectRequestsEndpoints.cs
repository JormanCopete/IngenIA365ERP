using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Compliance.DataSubjectRequests;
using IngenIA365ERP.Domain.Enums.Compliance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Compliance;

/// <summary>
/// Las solicitudes del titular de los datos (feature 013, T69 b, T0088; contracts/api.md §22.3): la bandeja y el detalle con
/// <c>Compliance.DataSubjectRequests.Read</c>, radicar con <c>.Register</c>, prorrogar y responder con <c>.Respond</c>; toda escritura con
/// <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public sealed class DataSubjectRequestsEndpoints : ICarterModule
{
    public const string PermisoDeLectura = "Compliance.DataSubjectRequests.Read";
    public const string PermisoDeRadicacion = "Compliance.DataSubjectRequests.Register";
    public const string PermisoDeRespuesta = "Compliance.DataSubjectRequests.Respond";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var solicitudes = app.MapGroup("/api/compliance/data-subject-requests")
            .WithTags("Compliance / Solicitudes del titular")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        solicitudes.MapGet("/", async ([FromQuery] DataSubjectRequestStatus? status, [FromQuery] DataSubjectRequestKind? kind,
                    [FromQuery] Guid? personPublicId, [FromQuery] DateOnly? dueBefore, [FromQuery] int? page, [FromQuery] int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDataSubjectRequestsQuery(status, kind, personPublicId, dueBefore, page ?? 1, pageSize ?? 50), ct))
            .WithName("Compliance_DataSubjectRequests_List")
            .RequirePermission(PermisoDeLectura);

        solicitudes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDataSubjectRequestQuery(id), ct))
            .WithName("Compliance_DataSubjectRequests_Get")
            .RequirePermission(PermisoDeLectura);

        solicitudes.MapPost("/", async ([FromBody] RadicarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RegisterDataSubjectRequestCommand(body.PersonPublicId, body.Kind, body.Purpose, body.ContactChannel,
                    body.Channel ?? string.Empty, body.ReceivedAt, body.Description ?? string.Empty, body.AttachmentPublicIds)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/compliance/data-subject-requests/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Compliance_DataSubjectRequests_Register")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRadicacion);

        solicitudes.MapPost("/{id:guid}/extend", async (Guid id, [FromBody] ProrrogaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ExtendDataSubjectRequestCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Compliance_DataSubjectRequests_Extend")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRespuesta);

        solicitudes.MapPost("/{id:guid}/respond", async (Guid id, [FromBody] RespuestaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RespondDataSubjectRequestCommand(id, body.Outcome, body.Answer ?? string.Empty, body.AttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Compliance_DataSubjectRequests_Respond")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRespuesta);
    }

    /// <summary>El cuerpo de la radicación (§22.3); <c>contactChannel</c> se suma para revocar un canal.</summary>
    public sealed record RadicarRequest(
        Guid PersonPublicId,
        DataSubjectRequestKind Kind,
        DataAuthorizationPurpose? Purpose,
        DataAuthorizationChannel? ContactChannel,
        string? Channel,
        DateTime ReceivedAt,
        string? Description,
        IReadOnlyList<Guid>? AttachmentPublicIds);

    public sealed record ProrrogaRequest(string? Reason);

    public sealed record RespuestaRequest(DataSubjectRequestOutcome Outcome, string? Answer, Guid? AttachmentPublicId);
}
