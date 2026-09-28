using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.ElectronicInvoicing;

/// <summary>
/// Los documentos electrónicos (feature 012, I4, T746; contracts/api.md §24.4, §24.5; contracts/dian.md §6, §8, §12): la bandeja
/// (<c>GET /documents</c>, paginada, con el alcance que decide cada módulo fuente) y el detalle con versiones y transmisiones; un intento
/// <b>ahora</b> (<c>/retry</c>) y la consulta de estado (<c>/query-status</c>) con <c>Documents.Transmit</c>; los casos a, b y c de un
/// rechazado (<c>/correct</c>, <c>/replacement-draft</c>, <c>/replace</c>, <c>/cancel</c>) con <c>Documents.Correct</c> —b y c además el
/// permiso de confirmar de la clase, que depende del documento y lo responde el handler con 422 <c>ClassPermissionRequired</c> (§2.9)—; el
/// cambio al canal vigente (<c>/transmit-by-current-channel</c>) con <c>Documents.TransmitByCurrentChannel</c>; y el enlace firmado de 60 s
/// de un artefacto (<c>/download-link</c>), abierto sólo con la regla del dueño del adjunto (<c>Inventory.Sales.View</c> o
/// <c>Inventory.Purchases.View</c>) y el mismo 404 sin ella.
/// <para>
/// Toda escritura exige <c>Idempotency-Key</c>. <c>/retry</c> y <c>/query-status</c> la exigen también aunque sus comandos no la registren:
/// no abren una transacción alrededor de la llamada al canal, y repetirlos es inocuo por el arrendamiento de la fila y la regla del ambiguo
/// (contracts/dian.md §6.1). Ninguna llamada al canal ocurre fuera de esos dos. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </para>
/// </summary>
public class DocumentsEndpoints : ICarterModule
{
    public const string Ruta = "/api/electronic-invoicing/documents";
    private const string Ver = "ElectronicInvoicing.Documents.View";
    private const string Transmitir = "ElectronicInvoicing.Documents.Transmit";
    private const string Corregir = "ElectronicInvoicing.Documents.Correct";
    private const string TransmitirPorElCanalVigente = "ElectronicInvoicing.Documents.TransmitByCurrentChannel";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Electronic Invoicing Documents")
            .RequireAuthorization();

        // ------------------------------------------------------------------------------------------ consultas (§24.4) --
        group.MapGet("/", async (
                ElectronicDocumentStatus? status, ElectronicDocumentKind? kind, DateOnly? from, DateOnly? to, string? prefix, string? number,
                string? sourceModule, bool? contingency, bool? overdue, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListElectronicDocumentsQuery(status, kind, from, to, prefix, number, sourceModule, contingency, overdue,
                    page ?? 1, pageSize ?? 20), ct))
            .WithName("ElectronicInvoicing_Documents_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetElectronicDocumentQuery(id), ct))
            .WithName("ElectronicInvoicing_Documents_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        // ---------------------------------------------------------------------------------- transmisión (§24.4) --
        group.MapPost("/{id:guid}/retry", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new EmitElectronicDocumentCommand(id, RetryNow: true), ct))
            .WithName("ElectronicInvoicing_Documents_Retry")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Transmitir);

        group.MapPost("/{id:guid}/query-status", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new QueryElectronicDocumentStatusCommand(id), ct))
            .WithName("ElectronicInvoicing_Documents_QueryStatus")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Transmitir);

        group.MapPost("/{id:guid}/transmit-by-current-channel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new TransmitByCurrentChannelCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("ElectronicInvoicing_Documents_TransmitByCurrentChannel")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(TransmitirPorElCanalVigente);

        // ------------------------------------------------------------------------------- casos a, b y c (§24.5) --
        group.MapPost("/{id:guid}/correct", async (Guid id, CorregirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CorrectRejectedDocumentCommand(id, body.Reason ?? string.Empty, body.PartySnapshotChanges)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("ElectronicInvoicing_Documents_Correct")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Corregir);

        group.MapPost("/{id:guid}/replacement-draft", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateReplacementDraftCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created(result.Value.EditRoute, result.Value) : result;
            })
            .WithName("ElectronicInvoicing_Documents_ReplacementDraft")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Corregir);

        group.MapPost("/{id:guid}/replace", async (Guid id, ReemplazarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReplaceRejectedDocumentCommand(id, body.ReplacementDocumentPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("ElectronicInvoicing_Documents_Replace")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Corregir);

        group.MapPost("/{id:guid}/cancel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelRejectedDocumentCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("ElectronicInvoicing_Documents_Cancel")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Corregir);

        // ------------------------------------------------------------------------------- artefactos (§24.4, FR-068) --
        group.MapPost("/{id:guid}/download-link", async (Guid id, string? artifact, int? version, int? transmission, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetElectronicArtifactLinkQuery(id, artifact ?? string.Empty, version, transmission), ct))
            .WithName("ElectronicInvoicing_Documents_DownloadLink")
            .RequireRateLimiting(LimiteDeAdjuntos.Politica)
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(new AlgunPermiso("Inventory.Sales.View", "Inventory.Purchases.View"));
    }

    /// <summary>El cuerpo con sólo el motivo (caso c y cambio al canal vigente).</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo del caso a: el motivo y, si se corrigen, los datos de la copia fiscal de la contraparte.</summary>
    public sealed record CorregirRequest(string? Reason, CambiosDeContraparte? PartySnapshotChanges);

    /// <summary>El cuerpo del caso b: el borrador de reemplazo (preparado con <c>/replacement-draft</c> y editado por su ruta) y el motivo.</summary>
    public sealed record ReemplazarRequest(Guid ReplacementDocumentPublicId, string? Reason);
}
