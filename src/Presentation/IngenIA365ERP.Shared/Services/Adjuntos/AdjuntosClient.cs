using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Contabilidad;
using IngenIA365ERP.Shared.Services.Security;
using Microsoft.JSInterop;

namespace IngenIA365ERP.Shared.Services.Adjuntos;

/// <summary>
/// Feature 011: los adjuntos desde la interfaz. Pedir, renovar y confirmar una subida, pedir un enlace de
/// descarga, listar y borrar pasan por la API; <b>el archivo no</b>: lo sube y lo baja
/// <c>wwwroot/js/adjuntos.js</c>, directo contra el almacén, y a .NET sólo le llegan su descripción y su
/// huella (research R14).
///
/// <para>
/// No pone la cabecera <c>Authorization</c>: la pone <c>RenovacionDeSesionHandler</c>, que además renueva
/// y reintenta (<c>ElTokenDeSesionLoPoneElHandler</c>).
/// </para>
/// </summary>
public sealed class AdjuntosClient(HttpClient http, CentralAuthClient auth, IJSRuntime js)
{
    private const string Base = "/api/attachments";

    public Task<ResultadoContable<List<AdjuntoDto>>> ListarAsync(string ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct = default) =>
        EnviarAsync<List<AdjuntoDto>>(HttpMethod.Get,
            $"{Base}/by-owner?ownerEntityType={Uri.EscapeDataString(ownerEntityType)}&ownerEntityPublicId={ownerEntityPublicId}", null, ct);

    public async Task<ResultadoContable<AutorizacionDeSubidaDto>> SolicitarSubidaAsync(SolicitudDeSubidaRequest solicitud, CancellationToken ct = default) =>
        ConUrlAbsoluta(await EnviarAsync<AutorizacionDeSubidaDto>(HttpMethod.Post, $"{Base}/uploads", solicitud, ct));

    public async Task<ResultadoContable<AutorizacionDeSubidaDto>> RenovarSubidaAsync(Guid adjunto, CancellationToken ct = default) =>
        ConUrlAbsoluta(await EnviarAsync<AutorizacionDeSubidaDto>(HttpMethod.Post, $"{Base}/{adjunto}/upload-url", null, ct));

    public Task<ResultadoContable<ConfirmacionDeSubidaDto>> ConfirmarAsync(Guid adjunto, CancellationToken ct = default) =>
        EnviarAsync<ConfirmacionDeSubidaDto>(HttpMethod.Post, $"{Base}/{adjunto}/confirm", null, ct);

    public async Task<ResultadoContable<EnlaceDeDescargaDto>> EnlaceDeDescargaAsync(Guid adjunto, CancellationToken ct = default)
    {
        var r = await EnviarAsync<EnlaceDeDescargaDto>(HttpMethod.Post, $"{Base}/{adjunto}/download-link", null, ct);
        return r.IsSuccess && r.Value is { Url: { } url } enlace ? r with { Value = enlace with { Url = Absoluta(url) } } : r;
    }

    public Task<ResultadoContable<EmptyResponse>> BorrarAsync(Guid adjunto, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"{Base}/{adjunto}", null, ct);

    /// <summary>
    /// Un adjunto del formato anterior (cifrado por la aplicación) se baja por la API, que lo descifra.
    /// Quien llama libera la respuesta.
    /// </summary>
    public async Task<HttpResponseMessage> AbrirPorLaApiAsync(Guid adjunto, CancellationToken ct = default) =>
        await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"{Base}/{adjunto}"), HttpCompletionOption.ResponseHeadersRead, ct);

    // ------------------------------------------------------------------ navegador --

    /// <summary>Lee el archivo elegido en el input con ese id: descripción y huella, sin los bytes.</summary>
    public ValueTask<ArchivoElegido?> PrepararAsync(string idDelInput) =>
        js.InvokeAsync<ArchivoElegido?>("adjuntos.preparar", idDelInput);

    public ValueTask OlvidarAsync(ArchivoElegido archivo) => js.InvokeVoidAsync("adjuntos.olvidar", archivo.Id);

    /// <summary>Navega al enlace firmado; el almacén obliga a guardar el archivo con su nombre.</summary>
    public ValueTask DescargarAsync(string url, string nombre) => js.InvokeVoidAsync("adjuntos.descargar", url, nombre);

    /// <summary>
    /// Pide la autorización, sube el archivo directo al almacén y confirma. Devuelve el estado final y
    /// un mensaje listo para mostrar.
    /// </summary>
    public async Task<ResultadoDeSoporte> SubirAsync(string ownerEntityType, Guid ownerEntityPublicId, ArchivoElegido archivo, CancellationToken ct = default)
    {
        var autorizacion = await SolicitarSubidaAsync(
            new SolicitudDeSubidaRequest(ownerEntityType, ownerEntityPublicId, archivo.Name, archivo.Type, archivo.Size, archivo.Sha256), ct);
        if (!autorizacion.IsSuccess || autorizacion.Value is null)
        {
            await OlvidarAsync(archivo);
            return ResultadoDeSoporte.Fallo(autorizacion.ErrorMessage ?? "No se pudo autorizar la subida.");
        }
        return await EnviarYConfirmarAsync(archivo, autorizacion.Value, ct);
    }

    /// <summary>
    /// Reintenta una subida incompleta: tiene que ser el <b>mismo</b> archivo, porque la autorización nueva
    /// fija la misma huella.
    /// </summary>
    public async Task<ResultadoDeSoporte> ReintentarAsync(AdjuntoDto adjunto, ArchivoElegido archivo, CancellationToken ct = default)
    {
        if (!string.Equals(Convert.ToHexString(Convert.FromBase64String(archivo.Sha256)), adjunto.Sha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            await OlvidarAsync(archivo);
            return ResultadoDeSoporte.Fallo($"Ese no es el mismo archivo que «{adjunto.FileName}». Para subir otro, use «Agregar soporte».");
        }
        var autorizacion = await RenovarSubidaAsync(adjunto.PublicId, ct);
        if (!autorizacion.IsSuccess || autorizacion.Value is null)
        {
            await OlvidarAsync(archivo);
            return ResultadoDeSoporte.Fallo(autorizacion.ErrorMessage ?? "No se pudo renovar la subida.");
        }
        return await EnviarYConfirmarAsync(archivo, autorizacion.Value, ct);
    }

    private async Task<ResultadoDeSoporte> EnviarYConfirmarAsync(ArchivoElegido archivo, AutorizacionDeSubidaDto autorizacion, CancellationToken ct)
    {
        var subida = await js.InvokeAsync<ResultadoDeSubida>("adjuntos.subir", ct,
            archivo.Id, autorizacion.Upload.Url, autorizacion.Upload.Fields, autorizacion.Upload.FileField);
        if (!subida.Ok)
            return ResultadoDeSoporte.Fallo(MotivoDelAlmacen(subida));

        var confirmacion = await ConfirmarAsync(autorizacion.AttachmentPublicId, ct);
        if (!confirmacion.IsSuccess || confirmacion.Value is null)
            return ResultadoDeSoporte.Fallo(confirmacion.ErrorMessage ?? "El archivo llegó, pero no se pudo confirmar. Recargue la lista.");
        return confirmacion.Value.Status switch
        {
            EstadosDeAdjunto.Disponible => new ResultadoDeSoporte(true, $"«{archivo.Name}» quedó guardado."),
            EstadosDeAdjunto.Rechazado => ResultadoDeSoporte.Fallo($"«{archivo.Name}» se rechazó: {confirmacion.Value.RejectionReason}"),
            _ => ResultadoDeSoporte.Fallo($"«{archivo.Name}» todavía no aparece en el almacén. Recargue la lista en unos segundos."),
        };
    }

    /// <summary>Lo que dijo el almacén, en palabras de la pantalla (research R1: qué rechaza la firma).</summary>
    internal static string MotivoDelAlmacen(ResultadoDeSubida r) => r switch
    {
        { Status: 0 } => "No se pudo contactar el almacén de archivos. Revise la conexión; si persiste, avise a soporte.",
        { Code: "EntityTooLarge" or "EntityTooSmall" } => "El almacén rechazó el archivo: no tiene el tamaño autorizado.",
        { Code: "BadDigest" or "InvalidDigest" or "XAmzContentChecksumMismatch" }
            => "El archivo cambió mientras se subía. Vuelva a intentarlo.",
        { Status: 403 } => "El almacén rechazó el archivo: no coincide con lo autorizado o la autorización venció.",
        _ => $"El almacén rechazó el archivo (error {r.Status}{(r.Code is null ? "" : $", {r.Code}")}).",
    };

    // ------------------------------------------------------------------ transporte --

    private async Task<ResultadoContable<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoContable<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
            using var resp = await http.SendAsync(req, ct);
            return await ResultadoContable<T>.DesdeAsync(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoContable<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new ResultadoContable<T>(false, default, "Generic.RespuestaInesperada", $"El servidor respondió con un formato inesperado: {ex.Message}", 0, null);
        }
    }

    private ResultadoContable<AutorizacionDeSubidaDto> ConUrlAbsoluta(ResultadoContable<AutorizacionDeSubidaDto> r) =>
        r.IsSuccess && r.Value is { } a ? r with { Value = a with { Upload = a.Upload with { Url = Absoluta(a.Upload.Url) } } } : r;

    private string Absoluta(string url) => EnlacesFirmados.Absoluta(http, url);
}

/// <summary>El final de una subida, con el mensaje para la persona.</summary>
public sealed record ResultadoDeSoporte(bool Ok, string Mensaje)
{
    public static ResultadoDeSoporte Fallo(string mensaje) => new(false, mensaje);
}
