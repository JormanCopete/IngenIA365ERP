using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los documentos al deudor y su verificación (feature 013, US13, T0790; contracts/api.md §15, adaptadores-y-archivos.md §8.11; FR-131, FR-132, T80):
/// la lista y el detalle con <c>Lending.Documents.Read</c>; emitir en un paso (<c>POST /</c>, 201) y enviar por correo (<c>POST /{id}/send</c>, 201) con
/// <c>.Issue</c>; el enlace de 60 s del PDF guardado con <c>.Reprint</c> (sin clave, como el del archivo de desembolsos: repetirlo emite otro enlace y
/// cada uno queda auditado como reimpresión); anular con motivo con <c>.Void</c> (204); verificar un código con <c>.Verify</c>. Toda escritura lleva
/// <c>Idempotency-Key</c>. <b>No hay ruta anónima de verificación</b> (D21): el QR abre <c>/cartera/verificar</c>, que exige sesión. Además, el dueño de
/// las imágenes del documento (<c>GET /images</c>, <c>Lending.Parameters.View</c>; T0781, I6) para su carga en <c>/cartera/parametros</c>. (nuevo)
/// </summary>
public class DocumentsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Documents.Read";
    public const string Emitir = "Lending.Documents.Issue";
    public const string Reimprimir = "Lending.Documents.Reprint";
    public const string Anular = "Lending.Documents.Void";
    public const string Verificar = "Lending.Documents.Verify";
    public const string VerParametros = "Lending.Parameters.View";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var documentos = LendingGroup.Grupo(app, "/documents", "Cartera Documentos");

        documentos.MapGet("/", async (IssuedDocumentKind? kind, Guid? personPublicId, Guid? loanPublicId, Guid? accountPublicId, IssuedDocumentStatus? status,
                    DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListIssuedLendingDocumentsQuery(kind, personPublicId, loanPublicId, status, from, to, page ?? 1, pageSize ?? 20)
                {
                    AccountPublicId = accountPublicId,
                }, ct))
            .WithName("Lending_Documents_List")
            .RequirePermission(Leer);

        documentos.MapGet("/images", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingDocumentImagesQuery(), ct))
            .WithName("Lending_Documents_Images")
            .RequirePermission(VerParametros);

        // K5 (T0974, R149): ?sha256= compara la huella de un PDF calculada en el navegador con la guardada (pdfMatches); el archivo nunca sube.
        documentos.MapGet("/verify/{code}", async (string code, string? sha256, ISender sender, CancellationToken ct) =>
                await sender.Send(new VerifyLendingDocumentQuery(code, sha256), ct))
            .WithName("Lending_Documents_Verify")
            .RequirePermission(Verificar);

        documentos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetIssuedLendingDocumentQuery(id), ct))
            .WithName("Lending_Documents_Get")
            .RequirePermission(Leer);

        documentos.MapPost("/", async (EmisionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new IssueLendingDocumentCommand(body.Kind, body.PersonPublicId, body.LoanPublicId, body.CutoffDate,
                    body.Period is { } p ? new PeriodoDeDocumento(p.From, p.To) : null, body.Year, body.Delivery, body.IncludePermanentSavings ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                    // Feature 014, US13 (T198; documentos.md §3): la cuenta del extracto o del certificado de saldo y sus opciones.
                    AccountPublicId = body.AccountPublicId,
                    IncludeRestrictions = body.IncludeRestrictions ?? false,
                }, ct), d => $"/api/lending/documents/{d.DocumentPublicId}"))
            .WithName("Lending_Documents_Issue")
            .ConClaveDeOperacion()
            .RequirePermission(Emitir);

        documentos.MapPost("/{id:guid}/download-link", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IssueLendingDocumentDownloadLinkCommand(id), ct))
            .WithName("Lending_Documents_DownloadLink")
            .RequireRateLimiting(LimiteDeAdjuntos.Politica)
            .RequirePermission(Reimprimir);

        documentos.MapPost("/{id:guid}/send", async (Guid id, EnvioRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SendLendingDocumentCommand(id, body?.RecipientPersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/documents/{id}"))
            .WithName("Lending_Documents_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Emitir);

        documentos.MapPost("/{id:guid}/void", async (Guid id, AnulacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new VoidLendingDocumentCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Lending_Documents_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Anular);
    }

    /// <summary>
    /// El pedido de emisión (§15): clase, titular, crédito, fecha de corte del certificado de saldo, período o año (K5) y la entrega. Feature 014 (T198;
    /// documentos.md §3): la cuenta (extracto y certificado de saldo de una cuenta), los bloqueos y embargos del certificado y el ahorro permanente de la
    /// constancia de aportes.
    /// </summary>
    public sealed record EmisionRequest(
        IssuedDocumentKind Kind,
        Guid PersonPublicId,
        Guid? LoanPublicId,
        DateOnly? CutoffDate,
        PeriodoRequest? Period,
        short? Year,
        DocumentDeliveryMode Delivery,
        Guid? AccountPublicId = null,
        bool? IncludeRestrictions = null,
        bool? IncludePermanentSavings = null);

    /// <summary>El período de un extracto (K5).</summary>
    public sealed record PeriodoRequest(DateOnly From, DateOnly To);

    /// <summary>El destinatario del envío posterior (nulo = el titular).</summary>
    public sealed record EnvioRequest(Guid? RecipientPersonPublicId);

    /// <summary>El motivo de la anulación.</summary>
    public sealed record AnulacionRequest(string? Reason);
}
