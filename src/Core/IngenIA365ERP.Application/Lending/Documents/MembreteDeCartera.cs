using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>Una imagen vigente de Cartera (el logotipo o la firma) leída y comprobada contra su SHA-256: su adjunto, su tipo y sus bytes. (nuevo)</summary>
public sealed record ImagenDeCartera(Guid PublicId, string TipoDeContenido, byte[] Bytes)
{
    /// <summary>La imagen como <c>data:{tipo};base64,…</c>, para pintarla en una pantalla sin otra petición.</summary>
    public string DataUri => $"data:{(string.IsNullOrWhiteSpace(TipoDeContenido) ? "image/png" : TipoDeContenido)};base64,{Convert.ToBase64String(Bytes)}";
}

/// <summary>
/// El membrete de lo que imprime Cartera (2026-11-16, pedido del dueño): el nombre y el NIT con dígito de la cooperativa y las imágenes vigentes
/// (logotipo y firma). Sale de <see cref="EmisionDeDocumentosDeCartera"/>, que lo armaba adentro, para que el estado de cuenta de la persona y la
/// simulación de cancelación lleven el mismo membrete que los documentos emitidos sin repetir la lectura. Sólo lee. (nuevo)
/// </summary>
public static class MembreteDeCartera
{
    /// <summary>El nombre por defecto si la cooperativa no tiene empresa registrada.</summary>
    public const string CooperativaSinNombre = "Cooperativa";

    /// <summary>El nombre y el NIT (con su dígito de verificación, si lo tiene) de la primera empresa viva de la cooperativa.</summary>
    public static async Task<(string Nombre, string? Nit)> CooperativaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var cooperativa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.TaxId, c.TaxIdCheckDigit }).FirstOrDefaultAsync(ct);
        var nit = cooperativa is null || string.IsNullOrWhiteSpace(cooperativa.TaxId)
            ? null
            : string.IsNullOrWhiteSpace(cooperativa.TaxIdCheckDigit) ? cooperativa.TaxId : $"{cooperativa.TaxId}-{cooperativa.TaxIdCheckDigit}";
        return (cooperativa?.Name ?? CooperativaSinNombre, nit);
    }

    /// <summary>
    /// El logotipo o la imagen de la firma vigentes (T0781, I6): el último adjunto disponible de ese dueño —subir otro lo reemplaza—, leído y comprobado
    /// contra su SHA-256. Sin imagen, o si no se puede leer, nula: una imagen que falta no impide imprimir.
    /// </summary>
    public static async Task<ImagenDeCartera?> ImagenVigenteAsync(IApplicationDbContext db, IBlobStore store, IAttachmentCipher? cifrado, string tipo,
        CancellationToken ct)
    {
        var vigente = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == tipo && a.Status == EstadoDeAdjunto.Available && !a.IsDeleted)
            .OrderByDescending(a => a.Id).Select(a => (Guid?)a.PublicId).FirstOrDefaultAsync(ct);
        if (vigente is not { } id) return null;
        var leido = await ContenidoDeAdjunto.LeerAsync(db, store, cifrado, id, ct);
        return leido is null ? null : new ImagenDeCartera(id, leido.ContentType, leido.Bytes);
    }
}
