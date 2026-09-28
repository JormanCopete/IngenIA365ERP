using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing;

/// <summary>Los dos formatos de la representación gráfica (contracts/dian.md §13.1). (nuevo)</summary>
public enum FormatoDeRepresentacion
{
    /// <summary>Carta: factura, notas y documento soporte.</summary>
    Carta = 1,

    /// <summary>Tirilla de 80 mm: documento equivalente POS y su nota de ajuste.</summary>
    Tirilla80mm = 2,
}

/// <summary>
/// Lo que la plantilla de la representación gráfica necesita: el canónico (la contraparte es la de la copia fiscal, nunca el maestro de hoy),
/// el estado, el código único, el QR <b>tal como lo devolvió el canal</b> y las leyendas por estado. (nuevo)
/// </summary>
public sealed record SolicitudDeRepresentacion(
    DocumentoElectronicoCanonico Canonico,
    ElectronicDocumentStatus Estado,
    ContingencyType? Contingencia,
    string? UniqueCode,
    string? QrContent,
    IReadOnlyList<string> Leyendas,
    FormatoDeRepresentacion Formato);

/// <summary>
/// La representación gráfica del ERP (feature 012, I4, T719; contracts/dian.md §13.1): <b>una sola plantilla</b> para todos los canales y para el
/// modo propio —cambiar de proveedor no cambia documentos (FR-064)—, en carta y en tirilla de 80 mm. La implementa
/// <c>RepresentacionGraficaReport</c> en la API (QuestPDF + QRCoder, T750); Application sólo conoce este contrato. (nuevo)
/// </summary>
public interface IRepresentacionGraficaRenderer
{
    /// <summary>El PDF.</summary>
    byte[] Renderizar(SolicitudDeRepresentacion solicitud);
}

/// <summary>Las leyendas por estado de la representación (contracts/dian.md §13.1). (nuevo)</summary>
public static class LeyendasDeRepresentacion
{
    /// <summary>Documento de pruebas.</summary>
    public const string SinValidezFiscal = "SIN VALIDEZ FISCAL";

    /// <summary>
    /// Las leyendas que lleva un documento: en contingencia de la DIAN, «pendiente de validación de la DIAN»; en contingencia del facturador
    /// (papel, sin código único), el texto de la norma —por cotejar con la contadora (T761)—; en ambiente de pruebas, «SIN VALIDEZ FISCAL».
    /// </summary>
    public static IReadOnlyList<string> Para(ElectronicDocumentKind tipo, ElectronicDocumentStatus estado, ContingencyType? contingencia,
        DianEnvironment ambiente, bool tieneCodigoUnico)
    {
        var leyendas = new List<string>();
        if (estado == ElectronicDocumentStatus.DianContingency || (contingencia == ContingencyType.Dian04 && !Validado(estado)))
            leyendas.Add($"{Nombre(tipo)} – tipo {(int)ContingencyType.Dian04:00}, pendiente de validación de la DIAN");
        if (contingencia == ContingencyType.Issuer03 && !tieneCodigoUnico)
            leyendas.Add($"Documento expedido en contingencia (tipo {(int)ContingencyType.Issuer03:00}) por inconvenientes tecnológicos del facturador; " +
                "se transmitirá a la DIAN dentro del plazo que fija la norma.");
        if (ambiente != DianEnvironment.Production) leyendas.Add(SinValidezFiscal);
        return leyendas;
    }

    /// <summary>La tirilla para el POS; carta para lo demás.</summary>
    public static FormatoDeRepresentacion FormatoDe(ElectronicDocumentKind tipo) =>
        tipo is ElectronicDocumentKind.PosEquivalent or ElectronicDocumentKind.PosAdjustmentNote ? FormatoDeRepresentacion.Tirilla80mm : FormatoDeRepresentacion.Carta;

    private static bool Validado(ElectronicDocumentStatus e) => e is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices;

    private static string Nombre(ElectronicDocumentKind tipo) => tipo switch
    {
        ElectronicDocumentKind.Invoice => "Factura electrónica de venta",
        ElectronicDocumentKind.CreditNote => "Nota crédito electrónica",
        ElectronicDocumentKind.DebitNote => "Nota débito electrónica",
        ElectronicDocumentKind.PosEquivalent => "Documento equivalente electrónico",
        ElectronicDocumentKind.PosAdjustmentNote => "Nota de ajuste del documento equivalente",
        ElectronicDocumentKind.SupportDocument => "Documento soporte",
        ElectronicDocumentKind.SupportDocumentAdjustmentNote => "Nota de ajuste del documento soporte",
        _ => "Documento electrónico",
    };
}
