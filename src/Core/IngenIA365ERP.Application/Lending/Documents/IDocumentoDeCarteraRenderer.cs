using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// Pinta un documento emitido de Cartera en PDF (feature 013, US13, T0782; DT §2.9; adaptadores-y-archivos.md §8.10). La implementación con QuestPDF y
/// QRCoder (<c>DocumentoDeCarteraPdfRenderer</c>, T0789) la registra la API, la única capa que conoce esas librerías; molde
/// <c>ISettlementDocumentRenderer</c> de Nómina. No calcula nada ni lee la base: todo lo que imprime viene en el modelo, así el mismo modelo da los
/// mismos bytes. El PDF se pinta <b>una vez</b>, al emitir, y se guarda: reimprimir es descargar ese mismo archivo, nunca volver a pintar.
/// </summary>
public interface IDocumentoDeCarteraRenderer
{
    byte[] Render(DocumentoDeCarteraParaImprimir documento);
}

/// <summary>Lo que se imprime de un documento: el encabezado común y el cuerpo de su clase. (nuevo)</summary>
public sealed record DocumentoDeCarteraParaImprimir(EncabezadoDeDocumentoDeCartera Encabezado, CuerpoDeDocumentoDeCartera Cuerpo);

/// <summary>
/// El encabezado común (§8.10): membrete con el nombre y el NIT de la cooperativa (<c>COR_Companies</c>), la clase y su título, el número, la fecha
/// de emisión (hora de Colombia), el titular con la <b>copia</b> de su identificación, el firmante y su cargo (<c>Documentos.Firmante.*</c>), el
/// código de verificación en cuatro grupos, la URL de verificación que va en el QR y las leyendas. Con
/// <see cref="AmbienteDePruebas"/>, «AMBIENTE DE PRUEBAS – SIN VALIDEZ». <see cref="LogotipoPublicId"/> e <see cref="ImagenDeFirmaPublicId"/> son
/// los adjuntos opcionales de I6 (C90): nulos, el PDF lleva sólo nombre, cargo, código y QR. (nuevo)
/// </summary>
public sealed record EncabezadoDeDocumentoDeCartera(
    IssuedDocumentKind Clase,
    string Titulo,
    string Numero,
    DateTimeOffset EmitidoEn,
    string Cooperativa,
    string? NitDeLaCooperativa,
    string NombreDelTitular,
    string TipoDeDocumentoDelTitular,
    string DocumentoDelTitular,
    string Firmante,
    string CargoDelFirmante,
    string CodigoDeVerificacion,
    string UrlDeVerificacion,
    bool AmbienteDePruebas,
    IReadOnlyList<string> Leyendas)
{
    public const string MarcaDePruebas = "AMBIENTE DE PRUEBAS – SIN VALIDEZ";

    public byte[]? Logotipo { get; init; }
    public byte[]? ImagenDeFirma { get; init; }
    public Guid? LogotipoPublicId { get; init; }
    public Guid? ImagenDeFirmaPublicId { get; init; }
}

/// <summary>El cuerpo de una clase de documento. (nuevo)</summary>
public abstract record CuerpoDeDocumentoDeCartera;

/// <summary>Una cuota del plan impreso: capital, interés, seguros, otros cargos, cuota y saldo de capital. (nuevo)</summary>
public sealed record CuotaDelPlanImpresa(int Numero, DateOnly Vencimiento, decimal Capital, decimal Interes, decimal Seguros, decimal OtrosCargos, decimal Cuota,
    decimal SaldoDeCapital);

/// <summary>
/// El plan de pagos (§8.6): la versión del plan vigente en R, con las condiciones selladas y las tasas efectiva anual y nominal anual de su
/// periodicidad. (nuevo)
/// </summary>
public sealed record CuerpoDePlanDePagos(
    string NumeroDelCredito,
    string Linea,
    int VersionDelPlan,
    DateOnly VigenteDesde,
    decimal CapitalBase,
    decimal? TasaEfectivaAnual,
    decimal? TasaNominalAnual,
    string Periodicidad,
    int Cuotas,
    IReadOnlyList<CuotaDelPlanImpresa> Filas) : CuerpoDeDocumentoDeCartera
{
    public decimal TotalCuotas => Filas.Sum(f => f.Cuota);
}

/// <summary>Un crédito que certifica el paz y salvo, con el papel de la persona y su saldo al corte. (nuevo)</summary>
public sealed record CreditoCertificado(string Numero, string Linea, string Papel, decimal Saldo);

/// <summary>Una venta aceptada sin migrar que el paz y salvo general menciona como fuera de Cartera (FR-143). (nuevo)</summary>
public sealed record VentaFueraDeCartera(string Factura, DateOnly Fecha, decimal Valor);

/// <summary>
/// El paz y salvo de una obligación o general (§8.5, §8.6): los créditos certificados en cero al corte, el fin de la firmeza («La cancelación está
/// sujeta a firmeza hasta el {fecha}»), si incluyó las codeudas y las ventas fuera de Cartera. El general siempre lleva «Certifica sólo las
/// obligaciones de crédito de la cooperativa». (nuevo)
/// </summary>
public sealed record CuerpoDePazYSalvo(
    bool General,
    DateOnly Corte,
    IReadOnlyList<CreditoCertificado> Creditos,
    DateOnly? FirmezaHasta,
    bool? IncluyeCodeudas,
    IReadOnlyList<VentaFueraDeCartera> VentasFueraDeCartera) : CuerpoDeDocumentoDeCartera
{
    public const string LeyendaDeAlcance = "Certifica sólo las obligaciones de crédito de la cooperativa.";
}

/// <summary>Un concepto del certificado de saldo. (nuevo)</summary>
public sealed record ConceptoCertificado(string Concepto, decimal Valor);

/// <summary>
/// El certificado de saldo (§8.6): la liquidación a la fecha D con R = el instante de emisión (T24), por concepto, lo que cuesta cancelar, la huella
/// de la liquidación, la versión de reglas y, si D es futura, hasta cuándo vale («válido hasta {ValidaHasta}»). (nuevo)
/// </summary>
public sealed record CuerpoDeCertificadoDeSaldo(
    string NumeroDelCredito,
    string Linea,
    DateOnly Fecha,
    IReadOnlyList<ConceptoCertificado> PorConcepto,
    decimal ParaCancelar,
    int DiasDeMora,
    bool Proyectada,
    DateOnly? ValidaHasta,
    string HuellaDeLaLiquidacion,
    string VersionDeReglas) : CuerpoDeDocumentoDeCartera;

/// <summary>Los nombres de las clases y sus leyendas (§8.10). (nuevo)</summary>
public static class ModelosDeDocumentoDeCartera
{
    public const string LeyendaDeFirmeza = "La cancelación está sujeta a firmeza hasta el {0}.";
    public const string LeyendaDeVentasFueraDeCartera = "Las ventas a crédito {0} no hacen parte de Cartera y no se certifican aquí.";
    public const string LeyendaConCodeudas = "Incluye las obligaciones en las que la persona es codeudora.";
    public const string LeyendaSinCodeudas = "No incluye las obligaciones en las que la persona es codeudora.";
    public const string LeyendaDeProyeccion = "Liquidación proyectada sin pagos: válida hasta el {0}.";

    /// <summary>El título que lleva el documento.</summary>
    public static string Titulo(IssuedDocumentKind clase) => clase switch
    {
        IssuedDocumentKind.PaymentPlan => "Plan de pagos",
        IssuedDocumentKind.LoanClearance => "Paz y salvo",
        IssuedDocumentKind.GeneralClearance => "Paz y salvo general",
        IssuedDocumentKind.BalanceCertificate => "Certificado de saldo",
        IssuedDocumentKind.Statement => "Extracto",
        IssuedDocumentKind.TaxCertificate => "Certificado tributario",
        _ => clase.ToString(),
    };

    /// <summary>La URL de la página de verificación que va en el QR: <c>{urlDeLaAplicacion}/cartera/verificar?codigo=XXXX-XXXX-XXXX-XXXX</c>.</summary>
    public static string UrlDeVerificacion(string urlDeLaAplicacion, string codigoFormateado) =>
        $"{(urlDeLaAplicacion ?? string.Empty).TrimEnd('/')}/cartera/verificar?codigo={codigoFormateado}";

    /// <summary>El nombre del archivo del PDF: <c>{número}.pdf</c> en minúscula.</summary>
    public static string NombreDelArchivo(string numero) => $"{numero.ToLowerInvariant()}.pdf";
}
