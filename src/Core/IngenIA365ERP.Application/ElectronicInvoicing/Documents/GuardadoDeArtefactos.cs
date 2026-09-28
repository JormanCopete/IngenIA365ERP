using System.Text;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Los artefactos de un documento electrónico tal como los nombra la API (api.md §24.4, <c>artifact</c>): texto, no un enum guardado. (nuevo)
/// </summary>
public static class ArtefactosElectronicos
{
    public const string Canonical = "Canonical";
    public const string SignedXml = "SignedXml";
    public const string AttachedDocument = "AttachedDocument";
    public const string ApplicationResponse = "ApplicationResponse";
    public const string GraphicRepresentation = "GraphicRepresentation";

    public static readonly IReadOnlyList<string> Todos = [Canonical, SignedXml, AttachedDocument, ApplicationResponse, GraphicRepresentation];

    /// <summary>El artefacto por su nombre, sin distinguir mayúsculas; nulo si no es uno de los cinco.</summary>
    public static string? Normalizar(string? artefacto) =>
        Todos.FirstOrDefault(a => string.Equals(a, artefacto?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>El tramo del nombre de archivo (<c>{Number}-v{n}-{tramo}.{ext}</c>).</summary>
    public static string Tramo(string artefacto) => artefacto switch
    {
        Canonical => "canonico",
        SignedXml => "xml-firmado",
        AttachedDocument => "attached-document",
        ApplicationResponse => "application-response",
        _ => "representacion-grafica",
    };
}

/// <summary>
/// Guarda los artefactos de un documento electrónico como adjuntos <b>que genera el módulo</b> (feature 012, I4, T716; contracts/dian.md §4.1 y
/// §12): por <see cref="UploadAttachmentCommand"/> (formato <c>Direct</c>, <c>Available</c>, SHA-256), dentro del contexto de la cooperativa, con
/// dueño <c>ElectronicSalesDocument</c> o <c>ElectronicPurchaseDocument</c> según la clase y el <c>PublicId</c> del documento electrónico, y con
/// nombre <c>{Number}-v{n}-{artefacto}.{ext}</c>. Por versión: el canónico, el XML firmado, el <c>AttachedDocument</c> y el PDF, cuyas
/// referencias se llenan <b>una sola vez</b> (<see cref="ElectronicDocumentVersion.FijarArtefacto"/>); por transmisión, el
/// <c>ApplicationResponse</c>. Una subida que falla no tumba la transmisión: queda en la bitácora y se reintenta en el siguiente paso.
///
/// <para>
/// El canónico se sube <b>después</b> del commit de la confirmación: <see cref="CanonicoVerificadoAsync"/> lo vuelve a armar desde datos
/// inmutables y, si su SHA-256 no coincide con el de la versión, no se emite, se registra en Critical y sale <c>Dian.DocumentoSinValidar</c>
/// con la causa (§4.1).
/// </para>
/// (nuevo)
/// </summary>
public sealed class GuardadoDeArtefactos(ISender sender, IReconstruccionDelCanonico reconstruccion, IAlertas alertas, ILogger<GuardadoDeArtefactos> logger)
{
    /// <summary>Dueño de los artefactos de factura, notas, DEE y su nota (T41).</summary>
    public const string DuenoDeVentas = AdjuntosDeModulo.DocumentoElectronicoDeVenta;

    /// <summary>Dueño de los del documento soporte y su nota (y los eventos RADIAN de I5).</summary>
    public const string DuenoDeCompras = AdjuntosDeModulo.DocumentoElectronicoDeCompra;

    public const string TipoJson = "application/json";
    public const string TipoXml = "application/xml";
    public const string TipoZip = "application/zip";
    public const string TipoPdf = "application/pdf";

    /// <summary>El dueño de los adjuntos según la clase del documento.</summary>
    public static string DuenoDe(ElectronicDocumentKind tipo) =>
        tipo is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote
            or ElectronicDocumentKind.RadianEvent030 or ElectronicDocumentKind.RadianEvent032
            ? DuenoDeCompras
            : DuenoDeVentas;

    /// <summary><c>{Number}-v{n}-{artefacto}.{ext}</c>.</summary>
    public static string NombreDe(string numero, int version, string artefacto, string contentType) =>
        $"{numero}-v{version}-{ArtefactosElectronicos.Tramo(artefacto)}.{Extension(contentType)}";

    private static string Extension(string contentType) => contentType.ToLowerInvariant() switch
    {
        TipoJson => "json",
        TipoZip => "zip",
        TipoPdf => "pdf",
        _ => "xml",
    };

    /// <summary>
    /// El canónico de la versión, reconstruido y comprobado contra su SHA-256; si la versión todavía no tiene el adjunto, lo sube y llena la
    /// referencia. Si no coincide: Critical, <c>Dian.DocumentoSinValidar</c> y <c>ElectronicInvoicing.Document.CanonicalMismatch</c>.
    /// </summary>
    public async Task<Result<DocumentoElectronicoCanonico>> CanonicoVerificadoAsync(ElectronicDocument documento, ElectronicDocumentVersion version,
        CancellationToken ct)
    {
        var reconstruido = await reconstruccion.ReconstruirAsync(documento, version, ct);
        if (reconstruido.IsFailure)
        {
            logger.LogCritical("[FE.CanonicoSinReconstruir] El documento {Numero} v{Version} no se pudo volver a armar: {Codigo} {Mensaje}",
                documento.Number, version.VersionNumber, reconstruido.Error.Code, reconstruido.Error.Message);
            await AlertarSinValidarAsync(documento, $"No se pudo volver a armar para transmitirlo: {reconstruido.Error.Message}", ct);
            return Result.Failure<DocumentoElectronicoCanonico>(reconstruido.Error);
        }

        if (!string.Equals(reconstruido.Value.CanonicalSha256, version.CanonicalSha256, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogCritical("[FE.CanonicoNoCoincide] El documento {Numero} v{Version} reconstruido da {Obtenido} y se registró {Esperado}: no se emite.",
                documento.Number, version.VersionNumber, reconstruido.Value.CanonicalSha256, version.CanonicalSha256);
            await AlertarSinValidarAsync(documento,
                "Al volver a armarlo no coincide con lo que se registró al confirmarlo (la huella del documento cambió): no se transmitió.", ct);
            return Result.Failure<DocumentoElectronicoCanonico>(ErroresDeDocumentosElectronicos.CanonicalMismatch(documento.Number));
        }

        if (version.CanonicalAttachmentPublicId is null
            && await GuardarAsync(documento, version.VersionNumber, ArtefactosElectronicos.Canonical, TipoJson,
                Encoding.UTF8.GetBytes(reconstruido.Value.Json), ct) is { } id)
            version.FijarArtefacto(ArtefactoDeVersion.Canonico, id);

        return Result.Success(reconstruido.Value.Documento);
    }

    /// <summary>
    /// Guarda lo que devolvió el canal: el XML firmado y el <c>AttachedDocument</c> en la versión (si no los tenía) y el
    /// <c>ApplicationResponse</c>, cuyo adjunto devuelve para la transmisión.
    /// </summary>
    public async Task<Guid?> GuardarDelCanalAsync(ElectronicDocument documento, ElectronicDocumentVersion version, ResultadoDeCanal resultado,
        CancellationToken ct)
    {
        Guid? respuesta = null;
        foreach (var a in resultado.Artefactos)
        {
            if (a.Bytes is not { Length: > 0 }) continue;
            switch (a.Tipo)
            {
                case TipoDeArtefacto.XmlFirmado when version.SignedXmlAttachmentPublicId is null:
                    if (await GuardarAsync(documento, version.VersionNumber, ArtefactosElectronicos.SignedXml, Tipo(a), a.Bytes, ct) is { } xml)
                        version.FijarArtefacto(ArtefactoDeVersion.XmlFirmado, xml);
                    break;
                case TipoDeArtefacto.AttachedDocument when version.AttachedDocumentAttachmentPublicId is null:
                    if (await GuardarAsync(documento, version.VersionNumber, ArtefactosElectronicos.AttachedDocument, Tipo(a), a.Bytes, ct) is { } ad)
                        version.FijarArtefacto(ArtefactoDeVersion.AttachedDocument, ad);
                    break;
                case TipoDeArtefacto.ApplicationResponse:
                    respuesta = await GuardarAsync(documento, version.VersionNumber, ArtefactosElectronicos.ApplicationResponse, Tipo(a), a.Bytes, ct);
                    break;
            }
        }
        return respuesta;
    }

    /// <summary>Sube un artefacto; nulo (y un aviso en la bitácora) si no se pudo.</summary>
    public async Task<Guid?> GuardarAsync(ElectronicDocument documento, int version, string artefacto, string contentType, byte[] contenido,
        CancellationToken ct)
    {
        try
        {
            var r = await sender.Send(new UploadAttachmentCommand(DuenoDe(documento.Kind), documento.PublicId,
                NombreDe(documento.Number, version, artefacto, contentType), contentType, contenido), ct);
            if (r.IsSuccess) return r.Value;
            logger.LogWarning("[FE.ArtefactoSinGuardar] {Artefacto} de {Numero} v{Version}: {Codigo} {Mensaje}",
                artefacto, documento.Number, version, r.Error.Code, r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[FE.ArtefactoSinGuardar] {Artefacto} de {Numero} v{Version}", artefacto, documento.Number, version);
        }
        return null;
    }

    /// <summary>El tipo MIME de un artefacto del canal: el que declara, o por la extensión de su nombre.</summary>
    private static string Tipo(ArtefactoDelCanal a) =>
        !string.IsNullOrWhiteSpace(a.ContentType) ? a.ContentType.Split(';')[0].Trim().ToLowerInvariant()
        : a.NombreDeArchivo.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? TipoZip : TipoXml;

    private async Task AlertarSinValidarAsync(ElectronicDocument documento, string causa, CancellationToken ct)
    {
        try
        {
            await alertas.LevantarAsync(new AlertaALevantar(
                TiposDeAlerta.DocumentoSinValidar,
                $"El documento electrónico {documento.Number} no se pudo transmitir",
                causa,
                EntityType: EntidadDelDocumento,
                EntityPublicId: documento.PublicId,
                DedupKey: ClaveSinValidar(documento.PublicId)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FE.AlertaSinLevantar] Dian.DocumentoSinValidar de {Numero}", documento.Number);
        }
    }

    /// <summary>El <c>EntityType</c> de las alertas de un documento electrónico.</summary>
    public const string EntidadDelDocumento = "ElectronicDocument";

    /// <summary>La condición única de <c>Dian.DocumentoSinValidar</c>: una por documento.</summary>
    public static string ClaveSinValidar(Guid documento) => $"{TiposDeAlerta.DocumentoSinValidar}:{documento:N}";
}
