using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.Local;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Attachments;

/// <summary>
/// Las rutas que imitan a S3 en desarrollo (feature 011, research R16; contracts/api.md §10). Con
/// <c>AttachmentStorage:Provider = Local</c>, las autorizaciones de subida y de descarga apuntan aquí,
/// y el cliente usa exactamente el mismo flujo que contra el almacén real.
///
/// <para>
/// Son <b>anónimas</b> como las URLs de S3: la autorización es el token, que sólo acuña el servidor y
/// que dice qué archivo, qué tipo, qué tamaño y hasta cuándo. Aun así, sólo se registran con el
/// proveedor local y <b>fuera de Production</b>: una ruta anónima no tiene por qué existir en producción.
/// Es «no Production» y no «sólo Development» para que las pruebas de integración las tengan.
/// </para>
///
/// <para>
/// No deciden nada: traducen el formulario a un comando y el token a una consulta (Principio III), y
/// quien valida es <c>IAlmacenLocal</c>.
/// </para>
/// </summary>
public sealed class LocalBlobEndpoints : ICarterModule
{
    public const string Prefijo = "/api/attachments/local-blob";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var configuracion = app.ServiceProvider.GetRequiredService<IConfiguration>();
        var ambiente = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!DebenExistir(configuracion, ambiente)) return;

        var group = app.MapGroup(Prefijo)
            .WithTags("Attachments (almacén local)")
            .AllowAnonymous()
            .RequireRateLimiting(LimiteDeAdjuntos.Politica);

        group.MapPost("/{token}", RecibirAsync)
            .WithName("Attachments_LocalBlob_Upload")
            .DisableAntiforgery();

        group.MapGet("/{token}", LeerAsync)
            .WithName("Attachments_LocalBlob_Download");
    }

    /// <summary>Proveedor local (el que rige si no se configura otro) y un ambiente que no es producción.</summary>
    public static bool DebenExistir(IConfiguration configuracion, IHostEnvironment ambiente) =>
        string.Equals(configuracion["AttachmentStorage:Provider"] ?? "Local", "Local", StringComparison.OrdinalIgnoreCase)
        && !ambiente.IsProduction();

    private static async Task<IResult> RecibirAsync(string token, HttpRequest request, ISender sender, HttpContext http, CancellationToken ct)
    {
        var archivo = request.HasFormContentType ? (await request.ReadFormAsync(ct)).Files.GetFile("file") : null;
        if (archivo is null)
            return (IResult)ErrorEnvelopeFilter.Translate(http,
                Result.Failure(AttachmentErrorCodes.Validation_FileEmpty, "No se recibió un archivo en el campo «file»."))!;

        var campos = request.Form
            .Where(c => c.Key != "file")
            .ToDictionary(c => c.Key, c => c.Value.ToString(), StringComparer.Ordinal);
        await using var contenido = archivo.OpenReadStream();

        var resultado = await sender.Send(new RecibirSubidaLocalCommand(token, campos, contenido), ct);
        // S3 responde 204 a un POST aceptado; aquí igual, para que el cliente no distinga.
        return resultado.IsSuccess ? Results.NoContent() : (IResult)ErrorEnvelopeFilter.Translate(http, resultado)!;
    }

    private static async Task<IResult> LeerAsync(string token, ISender sender, HttpContext http, CancellationToken ct)
    {
        var resultado = await sender.Send(new LeerBlobLocalQuery(token), ct);
        if (resultado.IsFailure)
            return (IResult)ErrorEnvelopeFilter.Translate(http, resultado)!;

        http.Response.Headers.CacheControl = "no-store";
        var archivo = resultado.Value;
        return Results.File(archivo.Contenido, archivo.ContentType, archivo.NombreDeArchivo);
    }
}
