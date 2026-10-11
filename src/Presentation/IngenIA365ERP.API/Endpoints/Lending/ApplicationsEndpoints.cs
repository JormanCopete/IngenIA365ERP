using System.Text.Json;
using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las solicitudes de crédito (feature 013, US2, T0408; contracts/api.md §4.2, FR-035–FR-042, FR-047): lista y detalle
/// (<c>Lending.Applications.Read</c>), el borrador con sus documentos (<c>.Create</c>), radicar (<c>.Submit</c>), el análisis y volver a
/// correr validaciones (<c>.Analyze</c>), el enrutamiento (<c>.Read</c>) y anular o desistir (<c>.Cancel</c>). Toda escritura lleva
/// <c>Idempotency-Key</c>; con Cartera sin activar responden <c>Lending.NotActivated</c> (§1.13). Fuera del alcance de oficina o línea, el
/// 404 de la solicitud. Las decisiones viven en <see cref="DecisionsEndpoints"/>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ApplicationsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Applications.Read";
    public const string Crear = "Lending.Applications.Create";
    public const string Radicar = "Lending.Applications.Submit";
    public const string Analizar = "Lending.Applications.Analyze";
    public const string Cancelar = "Lending.Applications.Cancel";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var solicitudes = LendingGroup.Grupo(app, "/applications", "Cartera Solicitudes");

        solicitudes.MapGet("/", async (CreditApplicationStatus? status, Guid? line, Guid? office, Guid? applicant, string? level, bool? mine,
                DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCreditApplicationsQuery(status, line, office, applicant, level, mine ?? false, from, to, page ?? 1,
                    pageSize ?? 20), ct))
            .WithName("Lending_Applications_List")
            .RequirePermission(Leer);

        solicitudes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCreditApplicationQuery(id), ct))
            .WithName("Lending_Applications_Get")
            .RequirePermission(Leer);

        solicitudes.MapGet("/{id:guid}/routing", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCreditApplicationRoutingQuery(id), ct))
            .WithName("Lending_Applications_Routing")
            .RequirePermission(Leer);

        solicitudes.MapPost("/", async (CreditApplicationInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateCreditApplicationCommand(body)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), s => $"/api/lending/applications/{s.PublicId}"))
            .WithName("Lending_Applications_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        // El cuerpo es el CreditApplicationInput con su rowVersion al lado (§4.2): se lee una vez y se reparte.
        solicitudes.MapPut("/{id:guid}", async (Guid id, JsonElement body, HttpContext http, IOptions<JsonOptions> json, ISender sender,
                CancellationToken ct) =>
            {
                var (input, version) = CuerpoConVersion(body, json.Value.SerializerOptions);
                return await sender.Send(new UpdateCreditApplicationCommand(id, input, version) { OperationKey = http.ClaveDeOperacion() }, ct);
            })
            .WithName("Lending_Applications_Update")
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        solicitudes.MapPost("/{id:guid}/documents", async (Guid id, DocumentoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AttachCreditApplicationDocumentCommand(
                    id, body.RequirementCode, body.AttachmentPublicId, body.DocumentDate, body.PersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/applications/{id}"))
            .WithName("Lending_Applications_AttachDocument")
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        solicitudes.MapDelete("/{id:guid}/documents/{documentId:guid}", async (Guid id, Guid documentId, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new RemoveCreditApplicationDocumentCommand(id, documentId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Applications_RemoveDocument")
            .ConClaveDeOperacion()
            .RequirePermission(Crear);

        solicitudes.MapPost("/{id:guid}/submit", async (Guid id, VersionRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SubmitCreditApplicationCommand(id, body?.RowVersion) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Applications_Submit")
            .ConClaveDeOperacion()
            .RequirePermission(Radicar);

        solicitudes.MapPost("/{id:guid}/analysis", async (Guid id, AnalisisRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RecordCreditApplicationAnalysisCommand(
                    id, body.Income, body.Expenses, body.ExternalInstallments, body.Notes, body.Recommendation, body.PersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Applications_Analysis")
            .ConClaveDeOperacion()
            .RequirePermission(Analizar);

        solicitudes.MapPost("/{id:guid}/checks/rerun", async (Guid id, RerunRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RerunCreditApplicationChecksCommand(id, body?.Checks) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Applications_RerunChecks")
            .ConClaveDeOperacion()
            .RequirePermission(Analizar);

        solicitudes.MapPost("/{id:guid}/cancel", async (Guid id, CancelarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelCreditApplicationCommand(id, body.Kind ?? string.Empty, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Applications_Cancel")
            .ConClaveDeOperacion()
            .RequirePermission(Cancelar);
    }

    /// <summary>
    /// El cuerpo del PUT (§4.2): la solicitud y su <c>rowVersion</c> en el mismo objeto. Un cuerpo ilegible es el 400
    /// <c>Request.BodyInvalid</c> de siempre (lo traduce el manejador de excepciones).
    /// </summary>
    private static (CreditApplicationInput Input, byte[]? Version) CuerpoConVersion(JsonElement body, JsonSerializerOptions opciones)
    {
        try
        {
            var input = body.Deserialize<CreditApplicationInput>(opciones)
                        ?? throw new BadHttpRequestException("Falta la solicitud.");
            var version = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("rowVersion", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetBytesFromBase64()
                : null;
            return (input, version);
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException)
        {
            throw new BadHttpRequestException(e.Message, e is JsonException ? e : new JsonException(e.Message, e));
        }
    }

    /// <summary>Un documento ya subido con dueño <c>LoanApplicationDocument</c> (§4.2); sin requisito es un documento adicional.</summary>
    public sealed record DocumentoRequest(string? RequirementCode, Guid AttachmentPublicId, DateOnly? DocumentDate, Guid? PersonPublicId);

    /// <summary>La versión del borrador que se leyó (§4.2, <c>Concurrency.StaleRowVersion</c>).</summary>
    public sealed record VersionRequest(byte[]? RowVersion);

    /// <summary>El análisis (§4.2): la capacidad corregida del solicitante (o de la parte nombrada) y la recomendación.</summary>
    public sealed record AnalisisRequest(
        decimal? Income,
        decimal? Expenses,
        decimal? ExternalInstallments,
        string? Notes,
        string? Recommendation,
        Guid? PersonPublicId);

    /// <summary>Las validaciones que vuelven a correr por su nombre de §4.2 (todas si no viene).</summary>
    public sealed record RerunRequest(IReadOnlyList<string>? Checks);

    /// <summary>Anular o desistir con motivo (FR-047).</summary>
    public sealed record CancelarRequest(string? Kind, string? Reason);
}
