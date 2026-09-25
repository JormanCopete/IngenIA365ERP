using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Imports;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Common;

/// <summary>
/// Las dos rutas de toda plantilla de importación sobre la ruta base de su catálogo (feature 012, T49, T159;
/// contracts/plantillas.md §0.3–§0.7, contracts/api.md §2.9), para que cada catálogo las reutilice en vez de
/// escribirlas otra vez. (nuevo)
/// <list type="bullet">
/// <item><c>GET {base}/template.xlsx</c> con el permiso de consulta: el libro vacío de
/// <see cref="CatalogoDePlantillas"/>. Con <c>?withData=true</c> y una consulta de datos, el mismo libro lleno con lo que
/// hoy tiene la cooperativa; exige además el permiso de exportar (si lo hay) y, con datos personales,
/// <c>Inventory.Reports.ExportPersonalData</c>, y se audita como exportación. Sin consulta de datos, <c>withData</c> es
/// el 404 de lo inexistente.</item>
/// <item><c>POST {base}/import?mode=review|apply</c> con el permiso de importar: multipart con el archivo (hasta 16 MB) y
/// <c>reason</c>, <c>Idempotency-Key</c> obligatoria y sobre de error; en <c>review</c> con <c>format=xlsx</c> devuelve el
/// mismo libro con las columnas <c>resultado</c> y <c>errores</c>. Sin comando de importación (plantillas 10 a 13 en
/// I1) no se publica: el <c>POST</c> es el 404 de lo inexistente.</item>
/// </list>
/// </summary>
public static class RutasDePlantilla
{
    /// <summary>El permiso adicional de la descarga con datos personales (§0.6).</summary>
    public const string PermisoDeDatosPersonales = "Inventory.Reports.ExportPersonalData";

    /// <summary>El evento de auditoría de la descarga con datos (T230).</summary>
    public const string EventoDeExportacion = "Inventory.Catalog.Exported";

    /// <summary>Un poco más que el tope del archivo: el cuerpo multipart lleva los separadores y el motivo.</summary>
    private const long TopeDelCuerpo = EjecutorDeImportacion.MaximoDeBytes + 256 * 1024;

    /// <param name="grupo">El grupo de la ruta base del catálogo (ya con <c>RequireAuthorization()</c>).</param>
    /// <param name="permisoDeDescarga">El de consulta del área (§0.7).</param>
    /// <param name="permisoDeImportacion">El de importar (§0.7); se ignora si no hay <paramref name="importar"/>.</param>
    /// <param name="clave">La clave de la plantilla en <see cref="CatalogoDePlantillas"/>.</param>
    /// <param name="nombre">Prefijo de los nombres de ruta (<c>Inventory_Products</c>).</param>
    /// <param name="importar">Arma el comando con el modo, el archivo, el motivo y la clave de idempotencia.</param>
    /// <param name="datos">La consulta de lo existente para <c>?withData=true</c>.</param>
    /// <param name="permisoDeExportacion">El de exportar del área (<c>Inventory.Catalog.Export</c>); nulo en Core (§0.6).</param>
    /// <param name="datosPersonales">Si la descarga con datos trae datos personales (vendedores, listas por cliente…).</param>
    public static RouteGroupBuilder MapPlantilla(
        this RouteGroupBuilder grupo,
        string permisoDeDescarga,
        string? permisoDeImportacion,
        string clave,
        string nombre,
        Func<ModoDeImportacion?, ArchivoDeImportacion, string?, Guid, IRequest<Result<ImportResultDto>>>? importar = null,
        Func<IRequest<Result<DatosDePlantilla>>>? datos = null,
        string? permisoDeExportacion = null,
        bool datosPersonales = false)
    {
        var plantilla = CatalogoDePlantillas.Por(clave);

        grupo.MapGet("/template.xlsx", async ([FromQuery] bool? withData, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var definicion = CatalogoDePlantillas.Por(clave).Definicion;
                var nombreBase = $"plantilla-{plantilla.Numero:00}-{clave.Replace('.', '-')}";
                if (withData != true)
                {
                    var vacia = PlantillaDeImportacion.Xlsx(definicion, nombreBase);
                    return Results.File(vacia.Contenido, vacia.TipoContenido, vacia.NombreArchivo);
                }

                if (datos is null
                    || (permisoDeExportacion is not null && !await PermissionAuthorizationFilter.TieneAsync(http, permisoDeExportacion))
                    || (datosPersonales && !await PermissionAuthorizationFilter.TieneAsync(http, PermisoDeDatosPersonales)))
                    return PermissionAuthorizationFilter.NotFoundEnvelope(http);

                var existentes = await sender.Send(datos(), ct);
                if (existentes.IsFailure) return ErrorEnvelopeFilter.Translate(http, existentes);

                await AuditoriaEncadenada.RegistrarAsync(http.RequestServices, new AuditLogCommand
                {
                    Action = EventoDeExportacion,
                    EntityType = clave,
                    Module = definicion.Modulo,
                    NewValues = new { catalog = clave, rows = existentes.Value.TotalDeFilas },
                }, ct);

                var llena = PlantillaDeImportacion.Xlsx(definicion, nombreBase + "-datos", existentes.Value);
                return Results.File(llena.Contenido, llena.TipoContenido, llena.NombreArchivo);
            })
            .WithName($"{nombre}_Template")
            .RequirePermission(permisoDeDescarga);

        if (importar is null || permisoDeImportacion is null) return grupo;

        grupo.MapPost("/import", async (HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                    return Sobre(http, ArchivosTabulares.Vacio, StatusCodes.Status400BadRequest);

                var formulario = await http.Request.ReadFormAsync(ct);
                var archivo = formulario.Files.FirstOrDefault();
                if (archivo is null || archivo.Length == 0)
                    return Sobre(http, ArchivosTabulares.Vacio, StatusCodes.Status400BadRequest);
                if (archivo.Length > EjecutorDeImportacion.MaximoDeBytes)
                    return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 16 MB.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status413PayloadTooLarge);

                var modo = Enum.TryParse<ModoDeImportacion>(http.Request.Query["mode"].ToString(), ignoreCase: true, out var m)
                    && Enum.IsDefined(m) ? m : (ModoDeImportacion?)null;
                var motivo = formulario["reason"].ToString();
                if (motivo.Length > 400) motivo = motivo[..400];

                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);
                var subido = new ArchivoDeImportacion(archivo.FileName, ms.ToArray());

                var resultado = await sender.Send(
                    importar(modo, subido, string.IsNullOrWhiteSpace(motivo) ? null : motivo, http.ClaveDeOperacion()), ct);

                var formato = http.Request.Query["format"].ToString();
                if (resultado.IsSuccess && resultado.Value.Mode == ModoDeImportacion.Review
                    && string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var revisado = PlantillaDeImportacion.ConResultados(subido, CatalogoDePlantillas.Por(clave).Definicion, resultado.Value);
                    return Results.File(revisado.Contenido, revisado.TipoContenido, revisado.NombreArchivo);
                }
                return (object)resultado;
            })
            .WithName($"{nombre}_Import")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDelCuerpo))
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(permisoDeImportacion);

        return grupo;
    }

    private static IResult Sobre(HttpContext http, Error error, int estado) =>
        Results.Json(new { code = error.Code, message = error.Message, traceId = http.TraceIdentifier }, statusCode: estado);
}
