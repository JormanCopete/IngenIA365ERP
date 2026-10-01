using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Genera y guarda la representación gráfica de una versión (feature 012, I4, T719; contracts/dian.md §13.1): al validarse o al expedirse en
/// contingencia, con la plantilla única del ERP (<see cref="IRepresentacionGraficaRenderer"/>), el canónico verificado de la versión, el estado y
/// sus leyendas, y el QR <b>tal como lo devolvió el canal</b>. El PDF queda como artefacto de la versión (<c>GraphicPdfAttachmentPublicId</c>,
/// una sola vez) y las reimpresiones usan el guardado. Si la versión ya lo tiene, no genera otro. Mientras la API no registre el renderizador
/// (T750), no genera. (nuevo)
/// </summary>
public sealed class GeneracionDeRepresentacion(IApplicationDbContext db, GuardadoDeArtefactos artefactos, ILogger<GeneracionDeRepresentacion> logger,
    IRepresentacionGraficaRenderer? renderer = null)
{
    /// <summary>El PDF generado (para entregarlo sin volver a leerlo), o nulo si ya existía o no hay renderizador.</summary>
    public async Task<byte[]?> GenerarAsync(ElectronicDocument documento, ElectronicDocumentVersion version, CancellationToken ct)
    {
        if (version.GraphicPdfAttachmentPublicId is not null) return null;
        if (renderer is null)
        {
            logger.LogDebug("[FE.SinRenderizador] {Numero}: la representación gráfica no está registrada en este proceso.", documento.Number);
            return null;
        }

        var canonico = await artefactos.CanonicoVerificadoAsync(documento, version, ct);
        if (canonico.IsFailure) return null;

        var pdf = renderer.Renderizar(new SolicitudDeRepresentacion(
            canonico.Value,
            documento.Status,
            documento.ContingencyType,
            documento.UniqueCode,
            documento.QrContent,
            LeyendasDeRepresentacion.Para(documento.Kind, documento.Status, documento.ContingencyType, documento.Environment,
                !string.IsNullOrWhiteSpace(documento.UniqueCode)),
            LeyendasDeRepresentacion.FormatoDe(documento.Kind)));

        if (await artefactos.GuardarAsync(documento, version.VersionNumber, ArtefactosElectronicos.GraphicRepresentation, GuardadoDeArtefactos.TipoPdf,
                pdf, ct) is { } id)
        {
            version.FijarArtefacto(ArtefactoDeVersion.RepresentacionGrafica, id);
            await db.SaveChangesAsync(ct);
        }
        return pdf;
    }
}
