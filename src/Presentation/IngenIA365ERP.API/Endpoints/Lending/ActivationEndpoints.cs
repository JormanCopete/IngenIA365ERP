using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La activación de Cartera (feature 013, T0234, C107; contracts/api.md §19.3): el estado con sus requisitos y la activación, con
/// <c>Lending.Activation.Execute</c>. La activación exige <c>Idempotency-Key</c> y el punto del acta (<c>sessionItemPublicId</c>, C61).
/// Desde US17 (T0312) va sobre <see cref="LendingGroup"/>; US8 (T0756) suma la plantilla <c>lending.provisional-collections</c>, la revisión de
/// cobros provisionales (multipart, con clave porque deja el adjunto; no guarda filas) y el reconocimiento del aviso de <c>DESC_CARTERA</c>
/// (<c>payrollWarningAcknowledged</c>, que la pantalla pregunta a Nómina: el comando no lee <c>PAY_</c>). Carter registra el módulo solo. Los roles sugeridos
/// (§19.4, T0300) van con <c>Security.Roles.Create</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ActivationEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Activation.Execute";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // US17 (T0312): sobre el grupo base de Cartera, que suma la auditoría del intento denegado y del rechazo de forma.
        var activacion = LendingGroup.Grupo(app, "/activation", "Cartera Activación");

        activacion.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new GetLendingActivationStatusQuery(), ct))
            .WithName("Lending_Activation_Status")
            .RequirePermission(Permiso);

        activacion.MapPost("/", async (ActivarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ActivateLendingCommand(
                    body.MigrationLoadPublicId, body.ActivationDate, body.ProvisionalCollectionsAttachmentPublicId,
                    body.SessionItemPublicId ?? Guid.Empty, body.AcknowledgeWarnings, body.Reason ?? string.Empty, body.PayrollWarningAcknowledged)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created("/api/lending/activation", r.Value) : r;
            })
            .WithName("Lending_Activation_Execute")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        // US8 (T0756): la plantilla de cobros provisionales, vacía (encabezados en la fila 1, instrucciones en otra hoja).
        activacion.MapGet("/provisional-collections/template.xlsx", async (ISender sender, CancellationToken ct) =>
            {
                var definicion = await sender.Send(new GetLendingTemplateQuery(DefinicionesDeMigracion.CobrosProvisionalesClave), ct);
                if (definicion.IsFailure) return (object)definicion;
                var libro = PlantillaDeImportacion.Xlsx(definicion.Value, "plantilla-cartera-cobros-provisionales");
                return Results.File(libro.Contenido, libro.TipoContenido, libro.NombreArchivo);
            })
            .WithName("Lending_Activation_ProvisionalCollectionsTemplate")
            .RequirePermission(Permiso);

        // US8 (T0756, C62): la revisión previa; multipart con el archivo (hasta 16 MB), deja el adjunto y no guarda filas.
        activacion.MapPost("/provisional-collections/review", async (HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                    return Results.Json(new { code = ArchivosTabulares.Vacio.Code, message = ArchivosTabulares.Vacio.Message, traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status400BadRequest);
                var formulario = await http.Request.ReadFormAsync(ct);
                var archivo = formulario.Files.FirstOrDefault();
                if (archivo is null || archivo.Length == 0)
                    return Results.Json(new { code = ArchivosTabulares.Vacio.Code, message = ArchivosTabulares.Vacio.Message, traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status400BadRequest);
                if (archivo.Length > EjecutorDeImportacion.MaximoDeBytes)
                    return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 16 MB.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);
                return (object)await sender.Send(new ReviewProvisionalCollectionsCommand(new ArchivoDeImportacion(archivo.FileName, ms.ToArray()))
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
            })
            .WithName("Lending_Activation_ReviewProvisionalCollections")
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(EjecutorDeImportacion.MaximoDeBytes + 256 * 1024))
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        // Feature 013, T0300 (T61; §19.4): los roles de Cartera desde sus plantillas, idempotente. El permiso es el de crear roles.
        activacion.MapPost("/suggested-roles", async (RolesSugeridosRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CreateSuggestedLendingRolesCommand(body?.TemplateKeys) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Activation_SuggestedRoles")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRoles);
    }

    /// <summary>El permiso de <c>POST /suggested-roles</c> (§19.4).</summary>
    public const string PermisoDeRoles = "Security.Roles.Create";

    /// <summary>El cuerpo de los roles sugeridos (§19.4): sin claves, las trece plantillas <c>cartera.*</c>.</summary>
    public sealed record RolesSugeridosRequest(IReadOnlyList<string>? TemplateKeys);

    /// <summary>El cuerpo de la activación (§19.3).</summary>
    public sealed record ActivarRequest(
        Guid MigrationLoadPublicId,
        DateOnly ActivationDate,
        Guid? ProvisionalCollectionsAttachmentPublicId,
        Guid? SessionItemPublicId,
        bool AcknowledgeWarnings,
        string? Reason,
        bool PayrollWarningAcknowledged = false);
}
