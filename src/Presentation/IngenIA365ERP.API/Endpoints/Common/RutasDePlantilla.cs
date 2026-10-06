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
///
/// <para>
/// Feature 013 (T82, T0089; contracts/plantillas.md §0.8): la sobrecarga general recibe la búsqueda de la definición, el nombre del
/// libro, el permiso de los datos personales y el evento de la descarga con datos, así la usan los catálogos de plataforma
/// (<see cref="CatalogoDePlantillasDePlataforma"/>) y los de Cartera. La de antes —atada a <see cref="CatalogoDePlantillas"/> de la 012,
/// con <see cref="PermisoDeDatosPersonales"/> y <see cref="EventoDeExportacion"/>— la llama con esos valores y no cambia a las dieciséis
/// plantillas.
/// </para>
/// </summary>
public static class RutasDePlantilla
{
    /// <summary>El permiso adicional de la descarga con datos personales (§0.6).</summary>
    public const string PermisoDeDatosPersonales = "Inventory.Reports.ExportPersonalData";

    /// <summary>El evento de auditoría de la descarga con datos (T230).</summary>
    public const string EventoDeExportacion = AuditEventTypes.InventoryCatalogExported;

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
    /// <param name="camposDelFormulario">
    /// Campos del formulario, además del motivo, que el comando necesita (feature 012, T286: <c>confirmFiscalWithoutPosting</c> de la
    /// plantilla 8): recibe el comando armado por <paramref name="importar"/> y el formulario, y devuelve el comando completo.
    /// </param>
    public static RouteGroupBuilder MapPlantilla(
        this RouteGroupBuilder grupo,
        string permisoDeDescarga,
        string? permisoDeImportacion,
        string clave,
        string nombre,
        Func<ModoDeImportacion?, ArchivoDeImportacion, string?, Guid, IRequest<Result<ImportResultDto>>>? importar = null,
        Func<IRequest<Result<DatosDePlantilla>>>? datos = null,
        string? permisoDeExportacion = null,
        bool datosPersonales = false,
        Func<IRequest<Result<ImportResultDto>>, IFormCollection, IRequest<Result<ImportResultDto>>>? camposDelFormulario = null)
    {
        var plantilla = CatalogoDePlantillas.Por(clave);
        return grupo.MapPlantilla(
            () => CatalogoDePlantillas.Por(clave).Definicion,
            $"plantilla-{plantilla.Numero:00}-{clave.Replace('.', '-')}",
            permisoDeDescarga, permisoDeImportacion, nombre, PermisoDeDatosPersonales, EventoDeExportacion,
            importar, datos, permisoDeExportacion, datosPersonales, camposDelFormulario);
    }

    /// <summary>
    /// Feature 013 (T82, T0089): las mismas dos rutas de una plantilla de un catálogo de plataforma (<see cref="CatalogoDePlantillasDePlataforma"/>),
    /// con el permiso de datos personales y el evento de exportación de su área.
    /// </summary>
    public static RouteGroupBuilder MapPlantillaDePlataforma(
        this RouteGroupBuilder grupo,
        string clave,
        string nombre,
        Func<ModoDeImportacion?, ArchivoDeImportacion, string?, Guid, IRequest<Result<ImportResultDto>>>? importar = null,
        Func<IRequest<Result<DatosDePlantilla>>>? datos = null,
        string? permisoDeDatosPersonales = null,
        string eventoDeExportacion = EventoDeExportacion)
    {
        var plantilla = CatalogoDePlantillasDePlataforma.Por(clave);
        return grupo.MapPlantilla(() => CatalogoDePlantillasDePlataforma.Por(clave).Definicion, plantilla.NombreDelLibro,
            plantilla.PermisoDeDescarga, plantilla.PermisoDeImportacion, nombre, permisoDeDatosPersonales ?? PermisoDeDatosPersonales,
            eventoDeExportacion, importar, datos, permisoDeExportacion: null, datosPersonales: permisoDeDatosPersonales is not null);
    }

    /// <summary>
    /// Feature 013 (T82, T0089; contracts/plantillas.md §0.8): la forma general. <paramref name="buscarDefinicion"/> se llama en cada
    /// petición (como antes la búsqueda en el catálogo de la 012); <paramref name="permisoDeDatosPersonales"/> se exige además en la descarga
    /// con datos cuando <paramref name="datosPersonales"/>; <paramref name="eventoDeExportacion"/> es la acción del evento de esa descarga.
    /// </summary>
    public static RouteGroupBuilder MapPlantilla(
        this RouteGroupBuilder grupo,
        Func<DefinicionDePlantilla> buscarDefinicion,
        string nombreDelLibro,
        string permisoDeDescarga,
        string? permisoDeImportacion,
        string nombre,
        string permisoDeDatosPersonales,
        string eventoDeExportacion,
        Func<ModoDeImportacion?, ArchivoDeImportacion, string?, Guid, IRequest<Result<ImportResultDto>>>? importar = null,
        Func<IRequest<Result<DatosDePlantilla>>>? datos = null,
        string? permisoDeExportacion = null,
        bool datosPersonales = false,
        Func<IRequest<Result<ImportResultDto>>, IFormCollection, IRequest<Result<ImportResultDto>>>? camposDelFormulario = null)
    {
        ArgumentNullException.ThrowIfNull(buscarDefinicion);

        grupo.MapGet("/template.xlsx", async ([FromQuery] bool? withData, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var definicion = buscarDefinicion();
                var nombreBase = nombreDelLibro;
                if (withData != true)
                {
                    var vacia = PlantillaDeImportacion.Xlsx(definicion, nombreBase);
                    return Results.File(vacia.Contenido, vacia.TipoContenido, vacia.NombreArchivo);
                }

                if (datos is null
                    || (permisoDeExportacion is not null && !await PermissionAuthorizationFilter.TieneAsync(http, permisoDeExportacion))
                    || (datosPersonales && !await PermissionAuthorizationFilter.TieneAsync(http, permisoDeDatosPersonales)))
                    return PermissionAuthorizationFilter.NotFoundEnvelope(http);

                var existentes = await sender.Send(datos(), ct);
                if (existentes.IsFailure) return ErrorEnvelopeFilter.Translate(http, existentes);

                await AuditoriaEncadenada.RegistrarAsync(http.RequestServices, new AuditLogCommand
                {
                    Action = eventoDeExportacion,
                    EntityType = definicion.Clave,
                    Module = definicion.Modulo,
                    NewValues = new { catalog = definicion.Clave, rows = existentes.Value.TotalDeFilas },
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

                var comando = importar(modo, subido, string.IsNullOrWhiteSpace(motivo) ? null : motivo, http.ClaveDeOperacion());
                if (camposDelFormulario is not null) comando = camposDelFormulario(comando, formulario);
                var resultado = await sender.Send(comando, ct);

                var formato = http.Request.Query["format"].ToString();
                if (resultado.IsSuccess && resultado.Value.Mode == ModoDeImportacion.Review
                    && string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var revisado = PlantillaDeImportacion.ConResultados(subido, buscarDefinicion(), resultado.Value);
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
