using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>El contenido de un adjunto leído del almacén: nombre, tipo y bytes ya comprobados contra su SHA-256. (nuevo)</summary>
public sealed record ContenidoDeAdjuntoLeido(string NombreDeArchivo, string ContentType, byte[] Bytes);

/// <summary>
/// Lee el contenido de un adjunto que el <b>servidor</b> necesita usar —el PDF de un documento emitido que viaja como adjunto de un correo al deudor
/// (feature 013, US13, T0786), el logotipo y la imagen de firma de los documentos (T0781)—, nunca para entregarlo a una persona (para eso está el
/// enlace firmado). Sólo un adjunto <c>Available</c>; el formato <c>Direct</c> se lee tal cual y el <c>AppEncrypted</c> se descifra con
/// <see cref="IAttachmentCipher"/>; el resultado se compara con su SHA-256. Nulo si no existe, no está disponible, falta el objeto o la huella no
/// cuadra: quien llama decide qué hacer (el despacho difiere el correo). (nuevo)
/// </summary>
public static class ContenidoDeAdjunto
{
    public static async Task<ContenidoDeAdjuntoLeido?> LeerAsync(IApplicationDbContext db, IBlobStore store, IAttachmentCipher? cifrado, Guid attachmentPublicId,
        CancellationToken ct)
    {
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == attachmentPublicId && !a.IsDeleted, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available) return null;

        byte[] bytes;
        try
        {
            await using var flujo = await store.GetAsync(new BlobReference(adjunto.StoragePath), ct);
            await using var memoria = new MemoryStream();
            await flujo.CopyToAsync(memoria, ct);
            bytes = memoria.ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted)
        {
            if (cifrado is null) return null;
            bytes = cifrado.Decrypt(bytes, adjunto.EncryptedDek);
        }
        var huella = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(huella, adjunto.Sha256Hex, StringComparison.OrdinalIgnoreCase)
            ? new ContenidoDeAdjuntoLeido(adjunto.FileName, adjunto.ContentType, bytes)
            : null;
    }
}
