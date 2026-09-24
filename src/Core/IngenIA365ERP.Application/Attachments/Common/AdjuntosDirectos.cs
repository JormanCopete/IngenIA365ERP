using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Lo que devuelve pedir o renovar una subida (contracts/api.md §1–§2): el cliente arma un formulario
/// con <b>todos</b> los <see cref="SubidaAutorizadaDto.Fields"/> y el archivo al final, en
/// <see cref="SubidaAutorizadaDto.FileField"/>, y lo envía directo al almacén.
/// </summary>
public sealed record AutorizacionDeSubidaDto(Guid AttachmentPublicId, SubidaAutorizadaDto Upload, long MaxBytes);

/// <param name="Url">
/// Dónde se envía el formulario. Con el almacén local es una ruta relativa a la API; el cliente la
/// resuelve contra su dirección base.
/// </param>
public sealed record SubidaAutorizadaDto(
    string Url, string Method, IReadOnlyDictionary<string, string> Fields, string FileField, DateTimeOffset ExpiresAt);

/// <summary>El resultado de confirmar una subida (§3). Confirmar es idempotente.</summary>
public sealed record ConfirmacionDeSubidaDto(EstadoDeAdjunto Status, string? RejectionReason, DateTime? ConfirmedAt);

/// <summary>
/// Un enlace de descarga (§5, §9). <see cref="Direct"/> falso significa «formato anterior»: el cliente
/// baja el archivo por la API, como antes.
/// </summary>
public sealed record EnlaceDeDescargaDto(bool Direct, string? Url, DateTimeOffset? ExpiresAt)
{
    public static readonly EnlaceDeDescargaDto PorLaApi = new(false, null, null);
}

/// <summary>
/// Feature 011: lo que comparten las rutas de subida y de descarga directas. Firmar un enlace de
/// descarga vive en <b>un solo sitio</b> —adjuntos, PILA y dispersión pasan por aquí— para que la vida
/// del enlace y la forma de nombrar el archivo no se dispersen.
/// </summary>
public static class AdjuntosDirectos
{
    /// <summary>Cuánto del principio se lee al confirmar (research R6).</summary>
    public const int BytesAExaminar = FirmaDeContenido.BytesAExaminar;

    /// <summary>
    /// Firma un enlace de descarga de <paramref name="adjunto"/> que vence en
    /// <see cref="LimitesDeAdjuntos.DescargaSegundos"/>. <paramref name="contentType"/> y
    /// <paramref name="nombre"/> permiten que un módulo imponga los suyos (la PILA lleva su charset).
    /// </summary>
    public static async Task<EnlaceDeDescargaDto> FirmarDescargaAsync(
        IBlobStore store, Attachment adjunto, IDateTimeService reloj, LimitesDeAdjuntos limites, CancellationToken ct,
        string? contentType = null, string? nombre = null)
    {
        var vence = Utc(reloj.UtcNow).AddSeconds(limites.DescargaSegundos);
        var enlace = await store.FirmarDescargaAsync(new BlobReference(adjunto.StoragePath),
            new DescargaFirmada(nombre ?? adjunto.FileName, contentType ?? adjunto.ContentType, vence), ct);
        return new EnlaceDeDescargaDto(true, enlace.Url, enlace.VenceEn);
    }

    /// <summary>
    /// La solicitud de firma que describe una fila ya registrada: su tipo, su tamaño y su huella exactos.
    /// Con <paramref name="mismaClave"/> se vuelve a firmar la clave que la fila ya tiene (renovar).
    /// </summary>
    public static SolicitudDeSubida Solicitud(Attachment adjunto, DateTimeOffset vence, bool mismaClave = false) =>
        new(new BlobMetadata(adjunto.TenantId.ToString(), adjunto.OwnerEntityType, adjunto.OwnerEntityPublicId,
                adjunto.FileName, adjunto.ContentType, adjunto.SizeBytes, adjunto.Sha256Hex),
            HexABase64(adjunto.Sha256Hex), vence, mismaClave ? new BlobReference(adjunto.StoragePath) : null);

    public static AutorizacionDeSubidaDto Dto(Attachment adjunto, AutorizacionDeSubida firma, LimitesDeAdjuntos limites) =>
        new(adjunto.PublicId,
            new SubidaAutorizadaDto(firma.Url, "POST",
                firma.Campos.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal),
                firma.CampoDelArchivo, firma.VenceEn),
            limites.MaxBytes);

    /// <summary>
    /// «No se puede con el estado actual» (409): renovar algo que no está incompleto, bajar algo que no
    /// está disponible. <c>data.status</c> dice en qué estado está.
    /// </summary>
    public static Error NoDisponible(Attachment adjunto, string mensaje) =>
        new ErrorConDatos(AttachmentErrorCodes.NotAvailable, mensaje, new { status = adjunto.Status });

    /// <summary>Verdadero para una subida cuya autorización ya venció sin que nadie la confirmara (R5).</summary>
    public static bool AutorizacionVencida(Attachment adjunto, DateTime ahora) =>
        adjunto.Status == EstadoDeAdjunto.Uploading && adjunto.UploadExpiresAt is { } vence && vence <= ahora;

    public static string HexABase64(string hex) => Convert.ToBase64String(Convert.FromHexString(hex));

    public static string Base64AHex(string base64) => Convert.ToHexString(Convert.FromBase64String(base64)).ToLowerInvariant();

    /// <summary>Una huella SHA-256 en base64: 32 bytes, 44 caracteres.</summary>
    public static bool EsHuellaValida(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64) || base64.Length != 44) return false;
        Span<byte> bytes = stackalloc byte[33];
        return Convert.TryFromBase64String(base64, bytes, out var n) && n == 32;
    }

    /// <summary>El nombre sin carpetas: algunos navegadores viejos mandan la ruta entera.</summary>
    public static string NombreLimpio(string nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        var corte = limpio.LastIndexOfAny(['/', '\\']);
        return corte >= 0 ? limpio[(corte + 1)..] : limpio;
    }

    public static DateTimeOffset Utc(DateTime instante) =>
        new(DateTime.SpecifyKind(instante, DateTimeKind.Utc));
}
