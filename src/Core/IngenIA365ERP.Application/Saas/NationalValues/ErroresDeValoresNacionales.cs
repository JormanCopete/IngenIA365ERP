using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Application.Saas.NationalValues;

/// <summary>
/// Los errores de la familia <c>NationalValues.*</c> (feature 013; data-model parte 3 §10.2; contracts/api.md §21.3, §21.5):
/// los del registro central del maestro, los de la copia en la cooperativa y el faltante que levantan los lectores. Los
/// mensajes son los del contrato. (nuevo)
/// </summary>
public static class ErroresDeValoresNacionales
{
    public const string CodeUnknownCode = "NationalValues.CodeUnknown";
    public const string KeyInvalidCode = "NationalValues.KeyInvalid";
    public const string CodeNotAvailableCode = "NationalValues.CodeNotAvailable";
    public const string AlreadyRegisteredCode = "NationalValues.Value.AlreadyRegistered";
    public const string AlreadyCorrectedCode = "NationalValues.Value.AlreadyCorrected";
    public const string UnitInvalidCode = "NationalValues.UnitInvalid";
    public const string ValueInvalidCode = "NationalValues.ValueInvalid";
    public const string DocumentInvalidCode = "NationalValues.Document.Invalid";
    public const string DocumentAlreadyRegisteredCode = "NationalValues.Document.AlreadyRegistered";
    public const string DocumentKindNotAvailableCode = "NationalValues.DocumentKindNotAvailable";
    public const string NotFoundCode = "NationalValues.NotFound";
    public const string DocumentNotFoundCode = "NationalValues.DocumentNotFound";
    public const string ConflictingValueCode = "NationalValues.ConflictingValue";
    public const string MissingCode = "NationalValues.Missing";
    public const string AlreadyReviewedCode = "NationalValues.AlreadyReviewed";
    public const string NoteRequiredCode = "NationalValues.Review.NoteRequired";

    public static Error CodigoDesconocido(string? codigo) =>
        new ErrorConDatos(CodeUnknownCode, $"«{codigo}» no es un código de valor nacional.", new { code = codigo });

    public static Error ClaveInvalida(string codigo, string mensaje, IReadOnlyList<string>? clavesAdmitidas = null) =>
        new ErrorConDatos(KeyInvalidCode, mensaje, new { code = codigo, allowedKeys = clavesAdmitidas });

    public static Error CodigoNoDisponible(string codigo) =>
        new ErrorConDatos(CodeNotAvailableCode,
            $"{codigo} todavía no está disponible (llega con la entrega {CodigosDeValorNacional.Buscar(codigo)?.DisponibleDesde.ToString() ?? "posterior"}).",
            new { code = codigo, availableFrom = CodigosDeValorNacional.Buscar(codigo)?.DisponibleDesde.ToString() });

    public static Error UnidadInvalida(string mensaje) => new(UnitInvalidCode, mensaje);

    public static Error ValorInvalido(string mensaje) => new(ValueInvalidCode, mensaje);

    /// <summary>T38: el mismo (<c>Code</c>, <c>Key</c>, <c>ValidFrom</c>) que una fila vigente sin reemplazar va como corrección.</summary>
    public static Error YaRegistrado(string codigo, string clave, DateOnly desde) =>
        new ErrorConDatos(AlreadyRegisteredCode,
            $"Ya existe {codigo} {clave} desde el {desde:yyyy-MM-dd}: regístrelo como corrección, con su motivo.".Replace("  ", " "),
            new { code = codigo, key = clave, validFrom = desde });

    /// <summary>Una fila ya reemplazada no se corrige otra vez: se corrige la corrección (único <c>SupersedesPublicId</c>). (nuevo)</summary>
    public static Error YaCorregido(Guid reemplazadaPor) =>
        new ErrorConDatos(AlreadyCorrectedCode,
            "Ese valor ya fue corregido: si hay que cambiarlo otra vez, corrija la corrección.", new { supersededBy = reemplazadaPor });

    /// <summary>T45: el contenido no cumple su esquema; la lista de errores con su ruta del JSON.</summary>
    public static Error DocumentoInvalido(IReadOnlyList<ErrorDeDocumentoNormativo> errores) =>
        new ErrorConDatos(DocumentInvalidCode, $"El documento no cumple su esquema: {errores.Count} errores.",
            new { errors = errores.Select(e => new { path = e.Ruta, message = e.Mensaje }).ToList() });

    public static Error DocumentoYaRegistrado(string clase, string codigo) =>
        new ErrorConDatos(DocumentAlreadyRegisteredCode,
            $"Ya existe el documento {clase} {codigo}: una versión nueva lleva otro código.", new { kind = clase, code = codigo });

    public static Error ClaseNoDisponible(string clase, string motivo) =>
        new ErrorConDatos(DocumentKindNotAvailableCode, $"Los documentos {clase} todavía no se pueden registrar: {motivo}.", new { kind = clase });

    public static readonly Error NoEncontrado = new(NotFoundCode, "No existe el valor nacional indicado.");

    public static readonly Error DocumentoNoEncontrado = new(DocumentNotFoundCode, "No existe el documento normativo indicado.");

    /// <summary>C98 (contracts/plantillas.md §10): la misma llave con otro valor y sin <c>corrigeA</c>.</summary>
    public static string MensajeDeValorEnConflicto(string codigo, string clave, DateOnly desde, decimal? valor) =>
        $"Ya existe {codigo} {clave} desde el {desde:yyyy-MM-dd} con otro valor ({valor?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "sin valor"}). "
        + "Si quiere cambiarlo, regístrelo como corrección (columna corrigeA) con su motivo.";

    /// <summary>T40: falta el valor de una fecha; lo que lo necesita se bloquea y se alerta <c>Cartera.ValorNacionalFaltante</c>.</summary>
    public static Error Faltante(string codigo, string clave, DateOnly fecha) =>
        new ErrorConDatos(MissingCode,
            $"Falta {codigo}{(clave.Length > 0 ? " " + clave : string.Empty)} para el {fecha:yyyy-MM-dd}. Pida al administrador de la plataforma registrarlo.",
            new { code = codigo, key = clave, date = fecha });

    public static Error YaRevisado() =>
        new(AlreadyReviewedCode, "Esa copia ya tiene esa misma revisión vigente.");

    public static readonly Error NotaObligatoria = new(NoteRequiredCode, "Una objeción lleva su nota: diga qué no coincide con la fuente.");
}
