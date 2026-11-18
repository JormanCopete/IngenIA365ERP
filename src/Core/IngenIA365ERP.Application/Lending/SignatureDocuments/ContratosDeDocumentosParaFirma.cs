using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;

namespace IngenIA365ERP.Application.Lending.SignatureDocuments;

/// <summary>Quien firma un documento: el deudor o un codeudor.</summary>
public sealed record FirmanteParaImprimir(string Nombre, string TipoDeDocumento, string Documento, string Calidad);

/// <summary>Un documento ya armado, listo para pintar: su nombre, su clase, sus bloques y quienes lo firman.</summary>
public sealed record DocumentoParaImprimir(
    string Nombre,
    SignatureDocumentKind Clase,
    IReadOnlyList<BloqueDeTexto> Bloques,
    IReadOnlyList<FirmanteParaImprimir> Firmantes,
    bool Huella);

/// <summary>
/// El juego para imprimir (2026-11-18): el membrete de la cooperativa, la referencia del pie, la marca de agua (ambiente de pruebas o vista previa) y
/// los documentos, cada uno desde una hoja nueva. (nuevo)
/// </summary>
public sealed record PaqueteParaFirma(
    string Cooperativa,
    string? Nit,
    byte[]? Logotipo,
    string Referencia,
    DateTimeOffset GeneradoEn,
    string? MarcaDeAgua,
    IReadOnlyList<DocumentoParaImprimir> Documentos);

/// <summary>Pinta un juego de documentos para firma en PDF (lo implementa la API con QuestPDF). No lee la base ni calcula. (nuevo)</summary>
public interface IDocumentosParaFirmaRenderer
{
    byte[] Render(PaqueteParaFirma paquete);
}

/// <summary>Los errores de los documentos para firma (2026-11-18). (nuevo)</summary>
public static class ErroresDeDocumentosParaFirma
{
    public const string CodigoPlantillaInexistente = "Lending.SignatureTemplate.NotFound";
    public const string CodigoPlantillaInvalida = "Lending.SignatureTemplate.Invalid";
    public const string CodigoCodigoDuplicado = "Lending.SignatureTemplate.CodeDuplicate";
    public const string CodigoCampoInexistente = "Lending.SignatureField.NotFound";
    public const string CodigoCampoDuplicado = "Lending.SignatureField.CodeDuplicate";
    public const string CodigoCampoReservado = "Lending.SignatureField.CodeReserved";
    public const string CodigoCampoEnUso = "Lending.SignatureField.InUse";
    public const string CodigoCodigoInmutable = "Lending.SignatureField.CodeImmutable";
    public const string CodigoSinPlantillas = "Lending.SignatureDocuments.NoTemplates";
    public const string CodigoNoAprobada = "Lending.SignatureDocuments.ApplicationNotApproved";
    public const string CodigoFaltanCampos = "Lending.SignatureDocuments.FieldsMissing";
    public const string CodigoValorInvalido = "Lending.SignatureDocuments.InvalidValue";

    public static Error PlantillaInexistente() => new(CodigoPlantillaInexistente, "La plantilla no existe.");

    public static Error PlantillaInvalida(string nombre, IReadOnlyList<ErrorDePlantilla> errores) => new ErrorConDatos(CodigoPlantillaInvalida,
        $"La plantilla «{nombre}» tiene errores: {string.Join(" ", errores.Take(5).Select(e => e.ToString()))}"
        + (errores.Count > 5 ? $" (y {errores.Count - 5} más)." : string.Empty),
        new { template = nombre, errors = errores.Select(e => new { line = e.Renglon, message = e.Mensaje }).ToList() });

    public static Error CodigoDuplicado(string codigo, string nombre) => new ErrorConDatos(CodigoCodigoDuplicado,
        $"Ya hay una plantilla con el código {codigo}: «{nombre}».", new { code = codigo, existing = nombre });

    public static Error CampoInexistente() => new(CodigoCampoInexistente, "El campo no existe.");

    public static Error CampoDuplicado(string codigo, string nombre) => new ErrorConDatos(CodigoCampoDuplicado,
        $"Ya hay un campo con el código {codigo}: «{nombre}».", new { code = codigo, existing = nombre });

    public static Error CampoReservado(string codigo) => new ErrorConDatos(CodigoCampoReservado,
        $"{codigo} es un campo del sistema (o termina en _LETRAS, que el sistema usa para los valores en pesos): elija otro código.", new { code = codigo });

    public static Error CampoEnUso(string codigo, IEnumerable<string> plantillas) => new ErrorConDatos(CodigoCampoEnUso,
        $"El campo {codigo} lo usan las plantillas {string.Join(", ", plantillas.Select(p => $"«{p}»"))}: quítelo de ellas antes de borrarlo.",
        new { code = codigo, templates = plantillas.ToList() });

    public static Error CodigoInmutable() => new(CodigoCodigoInmutable,
        "El código de un campo no cambia: las plantillas lo nombran. Cree otro campo con el código nuevo.");

    public static Error SinPlantillas() => new(CodigoSinPlantillas,
        "No hay plantillas de documentos para firma activas que apliquen a esta solicitud. Configúrelas en Cartera › Documentos para firma.");

    public static Error NoAprobada(string estado) => new ErrorConDatos(CodigoNoAprobada,
        $"La solicitud está en «{estado}»: los documentos para firma se generan con la solicitud aprobada y vigente.", new { status = estado });

    public static Error FaltanCampos(IReadOnlyList<(string Codigo, string Nombre)> faltan) => new ErrorConDatos(CodigoFaltanCampos,
        $"Faltan datos para generar los documentos: {string.Join(", ", faltan.Select(f => f.Nombre))}.",
        new { fields = faltan.Select(f => new { code = f.Codigo, label = f.Nombre }).ToList() });

    public static Error ValorInvalido(string nombre, string esperado) => new ErrorConDatos(CodigoValorInvalido,
        $"«{nombre}» debe ser {esperado}.", new { field = nombre });
}
