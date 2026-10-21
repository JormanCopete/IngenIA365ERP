using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.NationalValues;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Saas;

/// <summary>
/// El registro central de valores nacionales (feature 013, T37, B5; contracts/api.md §21.5): rutas <c>/api/saas/national-values</c>
/// y <c>/api/saas/regulatory-documents</c> del maestro con segundo factor (<see cref="RequireMasterAdminAttribute.Check"/>, sin
/// código de permiso), sobre de error común. Las escrituras exigen <c>Idempotency-Key</c>; como corren sin cooperativa no pasan por
/// <c>IdempotencyBehavior</c> (su tabla vive en la base de cada cooperativa): un reintento lo frenan las reglas del registro
/// (<c>NationalValues.Value.AlreadyRegistered</c>, «sin cambio» en la plantilla). Cada ruta sólo reenvía al <see cref="ISender"/>.
/// <b>Ningún cálculo lee lo que aquí se escribe</b>: la réplica lo copia a cada cooperativa. (nuevo)
/// </summary>
public sealed class NationalValuesEndpoints : ICarterModule
{
    /// <summary>Un poco más que el tope del archivo: el cuerpo multipart lleva los separadores.</summary>
    private const long TopeDelCuerpo = EjecutorDeImportacion.MaximoDeBytes + 256 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Valores(app);
        Documentos(app);
    }

    private static RouteGroupBuilder Grupo(IEndpointRouteBuilder app, string ruta) =>
        app.MapGroup(ruta)
            .WithTags("SaaS / Valores nacionales")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .AddEndpointFilter(async (contexto, siguiente) =>
            {
                var guarda = RequireMasterAdminAttribute.Check(contexto.HttpContext);
                return guarda.IsAllowed ? await siguiente(contexto) : Result.Failure(guarda.ErrorCode!, guarda.Message!);
            });

    private static void Valores(IEndpointRouteBuilder app)
    {
        var grupo = Grupo(app, "/api/saas/national-values");

        grupo.MapGet("/", async (string? code, string? key, DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPlatformNationalValuesQuery(code, key, from, to, page ?? 1, pageSize ?? 50), ct))
            .WithName("Saas_NationalValues_List");

        grupo.MapPost("/", async (RegistrarValorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RegisterNationalValueCommand(body.Code ?? string.Empty, body.Key, body.ValidFrom, body.ValidTo,
                    body.ReferenceDate, body.Value, body.Unit, body.Source ?? string.Empty, body.Label) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/saas/national-values/{r.Value}", new { nationalValuePublicId = r.Value }) : r;
            })
            .WithName("Saas_NationalValues_Register")
            .ConClaveDeOperacion();

        grupo.MapPost("/{id:guid}/corrections", async (Guid id, CorregirValorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CorrectNationalValueCommand(id, body.Value, body.ValidFrom, body.ValidTo, body.Source ?? string.Empty,
                    body.Reason ?? string.Empty, body.ReferenceDate, body.Label) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/saas/national-values/{r.Value}", new { nationalValuePublicId = r.Value }) : r;
            })
            .WithName("Saas_NationalValues_Correct")
            .ConClaveDeOperacion();

        grupo.MapGet("/missing", async (int? days, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMissingPlatformNationalValuesQuery(days ?? 31), ct))
            .WithName("Saas_NationalValues_Missing");

        grupo.MapGet("/template.xlsx", () =>
            {
                // Feature 013 (T0089): la definición y el nombre del libro salen del catálogo de plantillas de plataforma.
                var plantilla = CatalogoDePlantillasDePlataforma.Por(PlantillaDeValoresNacionales.Clave);
                var vacia = PlantillaDeImportacion.Xlsx(plantilla.Definicion, plantilla.NombreDelLibro);
                return Results.File(vacia.Contenido, vacia.TipoContenido, vacia.NombreArchivo);
            })
            .WithName("Saas_NationalValues_Template");

        grupo.MapPost("/import", async (HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType) return Result.Failure(ArchivosTabulares.Vacio);
                var formulario = await http.Request.ReadFormAsync(ct);
                var archivo = formulario.Files.FirstOrDefault();
                if (archivo is null || archivo.Length == 0) return Result.Failure(ArchivosTabulares.Vacio);
                if (archivo.Length > EjecutorDeImportacion.MaximoDeBytes)
                    return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 16 MB.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status413PayloadTooLarge);

                var modo = Enum.TryParse<ModoDeImportacion>(http.Request.Query["mode"].ToString(), ignoreCase: true, out var m) && Enum.IsDefined(m)
                    ? m : (ModoDeImportacion?)null;
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);
                var subido = new ArchivoDeImportacion(archivo.FileName, ms.ToArray());

                var resultado = await sender.Send(new ImportNationalValuesCommand(modo, subido) { OperationKey = http.ClaveDeOperacion() }, ct);
                if (resultado.IsSuccess && resultado.Value.Mode == ModoDeImportacion.Review
                    && string.Equals(http.Request.Query["format"].ToString(), "xlsx", StringComparison.OrdinalIgnoreCase)
                    && !subido.NombreArchivo.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var revisado = PlantillaDeImportacion.ConResultados(subido, CatalogoDePlantillasDePlataforma.Por(PlantillaDeValoresNacionales.Clave).Definicion, resultado.Value);
                    return Results.File(revisado.Contenido, revisado.TipoContenido, revisado.NombreArchivo);
                }
                return (object)resultado;
            })
            .WithName("Saas_NationalValues_Import")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDelCuerpo))
            .ConClaveDeOperacion();

        grupo.MapGet("/export", async (DateOnly? from, string? codes, ISender sender, CancellationToken ct) =>
            {
                var lista = string.IsNullOrWhiteSpace(codes) ? null : codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var r = await sender.Send(new ExportNationalValuesQuery(from, lista), ct);
                return r.IsSuccess ? (object)Results.File(r.Value.Contenido, r.Value.TipoDeContenido, r.Value.NombreArchivo) : r;
            })
            .WithName("Saas_NationalValues_Export");
    }

    private static void Documentos(IEndpointRouteBuilder app)
    {
        var grupo = Grupo(app, "/api/saas/regulatory-documents");

        grupo.MapGet("/", async (string? kind, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPlatformRegulatoryDocumentsQuery(kind), ct))
            .WithName("Saas_RegulatoryDocuments_List");

        grupo.MapPost("/", async (RegistrarDocumentoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RegisterRegulatoryDocumentCommand(body.Kind ?? string.Empty, body.Code ?? string.Empty, body.ValidFrom,
                    body.ValidTo, body.SchemaVersion, body.Content ?? string.Empty, body.Verified, body.Source ?? string.Empty, body.SupersedesPublicId, body.Reason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/saas/regulatory-documents/{r.Value}", new { regulatoryDocumentPublicId = r.Value }) : r;
            })
            .WithName("Saas_RegulatoryDocuments_Register")
            .ConClaveDeOperacion();

        grupo.MapGet("/{id:guid}/content", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new GetPlatformRegulatoryDocumentContentQuery(id), ct);
                return r.IsSuccess ? (object)Results.Content(r.Value.Content, "application/json") : r;
            })
            .WithName("Saas_RegulatoryDocuments_Content");
    }

    /// <summary>El alta de un valor (§21.5).</summary>
    public sealed record RegistrarValorRequest(
        string? Code, string? Key, DateOnly ValidFrom, DateOnly? ValidTo, DateOnly? ReferenceDate, decimal? Value, string? Unit, string? Source, string? Label);

    /// <summary>La corrección de un valor (§21.5).</summary>
    public sealed record CorregirValorRequest(
        decimal? Value, DateOnly? ValidFrom, DateOnly? ValidTo, DateOnly? ReferenceDate, string? Source, string? Reason, string? Label);

    /// <summary>El alta de un documento normativo (§21.5); <c>content</c> es el JSON como texto.</summary>
    public sealed record RegistrarDocumentoRequest(
        string? Kind, string? Code, DateOnly ValidFrom, DateOnly? ValidTo, short SchemaVersion, string? Content, bool Verified, string? Source,
        Guid? SupersedesPublicId, string? Reason);
}
