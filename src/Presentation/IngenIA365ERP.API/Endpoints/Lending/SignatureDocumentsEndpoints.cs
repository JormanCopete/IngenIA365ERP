using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.SignatureDocuments;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los documentos para firma de la solicitud de crédito (2026-11-18, pedido del dueño):
/// <list type="bullet">
/// <item>la configuración —plantillas, campos propios, carga de las plantillas base, validación del abogado y vista previa— con
/// <c>Lending.SignatureTemplates.Manage</c>; toda escritura con <c>Idempotency-Key</c> (la vista previa es una consulta por POST);</item>
/// <item>en la solicitud, lo que aplica y los juegos generados (<c>Lending.Applications.Read</c>) y generar (<c>Lending.PromissoryNotes.Manage</c>,
/// con <c>Idempotency-Key</c>). El PDF se baja por el enlace del adjunto, que pide <c>Lending.Applications.Read</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public class SignatureDocumentsEndpoints : ICarterModule
{
    public const string Configurar = "Lending.SignatureTemplates.Manage";
    public const string LeerSolicitudes = "Lending.Applications.Read";
    public const string Generar = PromissoryNotesEndpoints.Administrar;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var plantillas = LendingGroup.Grupo(app, "/signature-templates", "Cartera Documentos para firma");

        plantillas.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new GetSignatureConfigurationQuery(), ct))
            .WithName("Lending_SignatureTemplates_Get")
            .RequirePermission(Configurar);

        plantillas.MapPost("/", async (SignatureTemplateInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveSignatureTemplateCommand(null, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureTemplates_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        plantillas.MapPut("/{id:guid}", async (Guid id, SignatureTemplateInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveSignatureTemplateCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureTemplates_Update")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        plantillas.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteSignatureTemplateCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureTemplates_Delete")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        plantillas.MapPost("/{id:guid}/verify", async (Guid id, ValidacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VerifySignatureTemplateCommand(id, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureTemplates_Verify")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        plantillas.MapPost("/load-base", async (HttpContext http, ISender sender, CancellationToken ct) => await sender.Send(new LoadBaseSignatureTemplatesCommand { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureTemplates_LoadBase")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        plantillas.MapPost("/preview", async (VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
            {
                var pdf = await sender.Send(new PreviewSignatureTemplateQuery(body.Name ?? "Documento", body.Kind ?? "Other", body.Body ?? string.Empty,
                    body.IncludeFingerprint, body.SignedByCoDebtors, body.ApplicationPublicId, body.Values), ct);
                return pdf.IsSuccess ? (object)Results.File(pdf.Value, "application/pdf", "vista-previa.pdf") : pdf;
            })
            .WithName("Lending_SignatureTemplates_Preview")
            .RequirePermission(Configurar);

        var campos = LendingGroup.Grupo(app, "/signature-fields", "Cartera Documentos para firma");

        campos.MapPost("/", async (SignatureFieldInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveSignatureFieldCommand(null, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureFields_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        campos.MapPut("/{id:guid}", async (Guid id, SignatureFieldInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveSignatureFieldCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureFields_Update")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        campos.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) => await sender.Send(new DeleteSignatureFieldCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_SignatureFields_Delete")
            .ConClaveDeOperacion()
            .RequirePermission(Configurar);

        var solicitudes = LendingGroup.Grupo(app, "/applications", "Cartera Documentos para firma");

        solicitudes.MapGet("/{id:guid}/signature-documents", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetApplicationSignatureDocumentsQuery(id), ct))
            .WithName("Lending_Applications_SignatureDocuments")
            .RequirePermission(LeerSolicitudes);

        solicitudes.MapPost("/{id:guid}/signature-documents", async (Guid id, GenerarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new GenerateSignatureDocumentsCommand(id, body.Values) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Applications_SignatureDocuments_Generate")
            .ConClaveDeOperacion()
            .RequirePermission(Generar);
    }

    public sealed record ValidacionRequest(string? Note);

    public sealed record VistaPreviaRequest(
        string? Name,
        string? Kind,
        string? Body,
        bool IncludeFingerprint,
        bool SignedByCoDebtors,
        Guid? ApplicationPublicId,
        Dictionary<string, string?>? Values);

    public sealed record GenerarRequest(Dictionary<string, string?>? Values);
}
