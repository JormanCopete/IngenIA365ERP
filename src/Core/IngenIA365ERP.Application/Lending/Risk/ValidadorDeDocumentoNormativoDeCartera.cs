using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// El validador de los documentos normativos de Cartera (feature 013, T45, R2, T0204, T0691): <see cref="RegulatoryDocumentKind.SarcAnexo1"/>
/// (<see cref="Anexo1"/>), <see cref="RegulatoryDocumentKind.ReglaDePerdidaEsperada"/> (<see cref="Domain.Lending.Risk.Normativa.ReglaDePerdidaEsperada"/>)
/// y <see cref="RegulatoryDocumentKind.SarcAnexo2"/>, que hasta K6a rechaza todo contenido (la entrega desplegada no tiene el modelo de
/// pérdida esperada: <see cref="CatalogoDeCartera.EntregaVigente"/>). La plataforma no depende de Cartera: Cartera registra los suyos en
/// DI (uno por clase, <see cref="Todos"/>) y el registro central rechaza con <c>NationalValues.Document.Invalid</c> lo que no pase. (nuevo)
/// </summary>
public sealed class ValidadorDeDocumentoNormativoDeCartera : IValidadorDeDocumentoNormativo
{
    private ValidadorDeDocumentoNormativoDeCartera(RegulatoryDocumentKind clase) => Clase = clase;

    /// <summary>El del Anexo 1 del SARC.</summary>
    public static ValidadorDeDocumentoNormativoDeCartera Anexo1 { get; } = new(RegulatoryDocumentKind.SarcAnexo1);

    /// <summary>El de la regla de pérdida esperada.</summary>
    public static ValidadorDeDocumentoNormativoDeCartera ReglaDePerdidaEsperada { get; } = new(RegulatoryDocumentKind.ReglaDePerdidaEsperada);

    /// <summary>El del Anexo 2: hasta K6a rechaza todo contenido con el motivo.</summary>
    public static ValidadorDeDocumentoNormativoDeCartera Anexo2 { get; } = new(RegulatoryDocumentKind.SarcAnexo2);

    /// <summary>Los tres, para registrarlos o pasarlos a la semilla central.</summary>
    public static IReadOnlyList<IValidadorDeDocumentoNormativo> Todos { get; } = [Anexo1, ReglaDePerdidaEsperada, Anexo2];

    public RegulatoryDocumentKind Clase { get; }

    public IReadOnlyList<short> VersionesDeEsquema => Clase switch
    {
        RegulatoryDocumentKind.SarcAnexo1 => Domain.Lending.Risk.Normativa.Anexo1.VersionesDeEsquema,
        RegulatoryDocumentKind.ReglaDePerdidaEsperada => Domain.Lending.Risk.Normativa.ReglaDePerdidaEsperada.VersionesDeEsquema,
        _ => [],
    };

    public IReadOnlyList<ErrorDeDocumentoNormativo> Validar(string contenido, short versionDeEsquema) => Clase switch
    {
        RegulatoryDocumentKind.SarcAnexo1 => Domain.Lending.Risk.Normativa.Anexo1.Leer(contenido, versionDeEsquema).Errores,
        RegulatoryDocumentKind.ReglaDePerdidaEsperada => Domain.Lending.Risk.Normativa.ReglaDePerdidaEsperada.Leer(contenido, versionDeEsquema).Errores,
        _ => [new ErrorDeDocumentoNormativo("$",
            $"El Anexo 2 (pérdida esperada) llega con la entrega K6a; la desplegada es {CatalogoDeCartera.EntregaVigente}.")],
    };
}
