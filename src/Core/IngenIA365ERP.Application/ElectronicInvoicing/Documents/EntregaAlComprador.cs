using System.Globalization;
using System.IO.Compression;
using System.Net;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// La entrega del documento electrónico al comprador (feature 012, I4, T720; contracts/dian.md §13.2; FR-063, FR-067, SC-015): en operación
/// normal <b>sólo después</b> de <c>Validated</c> o <c>ValidatedWithNotices</c>; en contingencia, en el acto. Con <c>EmailDeliveryBy = Erp</c>
/// (sellado al numerar) envía por <see cref="IEmailSender"/> un ZIP con el <c>AttachedDocument</c> y el PDF de la representación a la dirección de
/// recepción de la <b>copia fiscal</b> (el canónico, nunca el maestro de hoy), con la plantilla <c>DocumentoElectronico</c>; con <c>Channel</c>
/// no envía —lo entrega el proveedor, nunca los dos— y sólo anota la entrega. Registra <c>DeliveredAt</c> y <c>EmailSentAt</c>. El asunto y el
/// nombre del adjunto siguen el anexo técnico y están <b>por cotejar</b> (T761). Sin autorización de datos (FR-011) el correo sólo se usa para
/// esto, que la ley exige. (nuevo)
/// </summary>
public sealed class EntregaAlComprador(
    IApplicationDbContext db,
    IEmailSender correo,
    IBlobStore almacen,
    IReconstruccionDelCanonico reconstruccion,
    IDateTimeService reloj,
    ILogger<EntregaAlComprador> logger,
    INotificationTemplateRenderer? plantillas = null)
{
    /// <summary>La plantilla (<c>Storage/EmailTemplates/DocumentoElectronico.cshtml</c>).</summary>
    public const string Plantilla = "DocumentoElectronico";

    /// <summary>¿Se puede entregar en este estado?</summary>
    public static bool Entregable(ElectronicDocumentStatus estado) => estado is ElectronicDocumentStatus.Validated
        or ElectronicDocumentStatus.ValidatedWithNotices or ElectronicDocumentStatus.DianContingency or ElectronicDocumentStatus.IssuerContingency;

    /// <summary>
    /// Entrega el documento. <paramref name="pdf"/> y <paramref name="attachedDocument"/> se usan si llegan; si no, se leen de los artefactos
    /// guardados. Con <paramref name="forzar"/> reenvía aunque ya se hubiera enviado (el «Reenviar correo» de la pantalla). Devuelve si salió
    /// un correo; <c>ElectronicInvoicing.Document.NotDeliverable</c> si todavía no se entrega.
    /// </summary>
    public async Task<Result<bool>> EntregarAsync(ElectronicDocument documento, ElectronicDocumentVersion version, byte[]? pdf, byte[]? attachedDocument,
        bool forzar, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (!Entregable(documento.Status)) return Result.Failure<bool>(ErroresDeDocumentosElectronicos.NotDeliverable(documento.Status));

        var ahora = reloj.UtcNow;
        if (documento.EmailDeliveryBy == EmailDeliveryBy.Channel)
        {
            // Lo entrega el proveedor: el ERP no envía (nunca los dos), sólo anota que quedó entregado por el canal.
            if (documento.DeliveredAt is null)
            {
                documento.DeliveredAt = ahora;
                await db.SaveChangesAsync(ct);
            }
            return Result.Success(false);
        }
        if (documento.EmailSentAt is not null && !forzar) return Result.Success(false);

        var canonico = await reconstruccion.ReconstruirAsync(documento, version, ct);
        if (canonico.IsFailure) return Result.Failure<bool>(canonico.Error);
        var comprador = canonico.Value.Documento.Counterparty;
        var destino = comprador.ReceptionEmail;
        if (string.IsNullOrWhiteSpace(destino))
        {
            logger.LogWarning("[FE.SinCorreoDeRecepcion] {Numero}: la copia fiscal no tiene correo de recepción; no se envió.", documento.Number);
            return Result.Success(false);
        }

        pdf ??= await LeerAsync(version.GraphicPdfAttachmentPublicId, ct);
        attachedDocument ??= await LeerAsync(version.AttachedDocumentAttachmentPublicId, ct);
        var emisor = canonico.Value.Documento.Issuer;

        // Asunto y nombre del adjunto según el anexo técnico (NIT;nombre;número;tipo;nombre): POR COTEJAR (T761).
        var asunto = string.Join(';', emisor.TaxId, emisor.Name, documento.Number, documento.DianDocumentTypeCode, emisor.Name);
        var adjuntos = new List<EmailAttachment>();
        var zip = Empaquetar(documento.Number, attachedDocument, pdf);
        if (zip is not null) adjuntos.Add(new EmailAttachment($"{documento.Number}.zip", GuardadoDeArtefactos.TipoZip, zip));

        var modelo = new Dictionary<string, string?>
        {
            ["Comprador"] = comprador.Name,
            ["Emisor"] = emisor.Name,
            ["Numero"] = documento.Number,
            ["Fecha"] = documento.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["Total"] = documento.TotalAmount.ToString("N2", CultureInfo.GetCultureInfo("es-CO")),
            ["CodigoUnico"] = documento.UniqueCode ?? "pendiente de validación de la DIAN",
        };
        var html = plantillas is null ? null : await plantillas.RenderHtmlAsync(Plantilla, modelo, ct);
        html ??= $"<p>{WebUtility.HtmlEncode(comprador.Name)}:</p><p>Adjuntamos el documento electrónico {WebUtility.HtmlEncode(documento.Number)} " +
                 $"de {WebUtility.HtmlEncode(emisor.Name)}.</p>";

        await correo.SendAsync(new EmailMessage(destino!, asunto, html, Attachments: adjuntos), ct);
        documento.EmailSentAt = ahora;
        documento.DeliveredAt ??= ahora;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    /// <summary>
    /// El ZIP del correo: el <c>AttachedDocument</c> (si ya viene comprimido, su contenido) y el PDF. Nulo si no hay nada que enviar.
    /// </summary>
    public static byte[]? Empaquetar(string numero, byte[]? attachedDocument, byte[]? pdf)
    {
        if (attachedDocument is not { Length: > 0 } && pdf is not { Length: > 0 }) return null;
        using var salida = new MemoryStream();
        using (var zip = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (attachedDocument is { Length: > 0 })
            {
                if (EsZip(attachedDocument))
                {
                    using var origen = new ZipArchive(new MemoryStream(attachedDocument), ZipArchiveMode.Read);
                    foreach (var entrada in origen.Entries)
                    {
                        using var lectura = entrada.Open();
                        using var escritura = zip.CreateEntry(entrada.FullName, CompressionLevel.Optimal).Open();
                        lectura.CopyTo(escritura);
                    }
                }
                else
                {
                    using var escritura = zip.CreateEntry($"{numero}.xml", CompressionLevel.Optimal).Open();
                    escritura.Write(attachedDocument);
                }
            }
            if (pdf is { Length: > 0 })
            {
                using var escritura = zip.CreateEntry($"{numero}.pdf", CompressionLevel.Optimal).Open();
                escritura.Write(pdf);
            }
        }
        return salida.ToArray();
    }

    private static bool EsZip(byte[] b) => b.Length > 3 && b[0] == (byte)'P' && b[1] == (byte)'K';

    private async Task<byte[]?> LeerAsync(Guid? adjunto, CancellationToken ct)
    {
        if (adjunto is not { } id) return null;
        var fila = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == id, ct);
        if (fila is null) return null;
        await using var flujo = await almacen.GetAsync(new BlobReference(fila.StoragePath), ct);
        using var copia = new MemoryStream();
        await flujo.CopyToAsync(copia, ct);
        return copia.ToArray();
    }
}
